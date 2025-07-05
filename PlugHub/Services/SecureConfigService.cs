using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlugHub.Services
{
    internal sealed class SecureValueJsonConverter : JsonConverter<SecureValue>
    {
        public override SecureValue? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            if (r.TokenType == JsonTokenType.String)
                return new SecureValue(r.GetString()!);

            if (r.TokenType == JsonTokenType.StartObject)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref r);
                string base64 = doc.RootElement.GetProperty("EncryptedBase64").GetString()!;
                return new SecureValue(base64);
            }
            throw new JsonException("Invalid SecureValue payload.");
        }
        public override void Write(Utf8JsonWriter w, SecureValue v, JsonSerializerOptions o) =>
            w.WriteStringValue(v.EncryptedBase64);
    }

    public class SecureConfigService(ILogger<IConfigService> logger, ITokenService tokenService, string configRootDirectory, string configUserDirectory)
        : ConfigService(logger, tokenService, configRootDirectory, configUserDirectory), ISecureConfigService, IDisposable
    {
        #region SecureConfigService: Registration

        public void RegisterConfig(Type configType, IEncryptionContext encryptionContext, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            bool hasSecureFields = configType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.PropertyType == typeof(SecureValue) || p.GetCustomAttribute<SecureAttribute>() != null);

            if (!hasSecureFields)
            {
                this.Logger.LogInformation(
                    "Type '{ConfigType}' contains no SecureValue properties; for faster startup consider using RegisterConfig instead.",
                    configType.Name);
            }

            jsonOptions ??= this.jsonOptions;

            string defaultConfigFilePath = this.GetDefaultSettingsPath(configType);

            this.EnsureEncryptedFileExists(defaultConfigFilePath, configType, encryptionContext);
            this.RegisterConfig(configType, ownerToken, readToken, writeToken, jsonOptions, reloadOnChange);
        }
        public void RegisterConfig(Type configType, IEncryptionContext encryptionContext, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            this.RegisterConfig(configType, encryptionContext, tokenSet.Owner, tokenSet.Read, tokenSet.Write, jsonOptions, reloadOnChange);
        }

        public void RegisterConfigs(IEnumerable<Type> configTypes, IEncryptionContext encryptionContext, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, encryptionContext, ownerToken, readToken, writeToken, jsonOptions, reloadOnChange);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, IEncryptionContext encryptionContext, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            this.RegisterConfigs(configTypes, encryptionContext, tokenSet.Owner, tokenSet.Read, tokenSet.Write, jsonOptions, reloadOnChange);
        }

        #endregion

        private protected override object? GetBuildSettingsValue(IConfiguration config, PropertyInfo prop)
        {
            bool isSecure = prop.PropertyType == typeof(SecureValue) || prop.GetCustomAttribute<SecureAttribute>() != null;

            if (isSecure)
            {
                IConfigurationSection section = config.GetSection(prop.Name);
                if (!section.Exists()) return null;
                string? base64 = section.Value ?? section["EncryptedBase64"];
                return base64 is null ? null : new SecureValue(base64);
            }

            // Fallback to base for non-secure
            return base.GetBuildSettingsValue(config, prop);
        }

        private void EnsureEncryptedFileExists(string filePath, Type configType, IEncryptionContext context)
        {
            if (File.Exists(filePath))
                return;

            EnsureDirectoryExists(filePath);

            Dictionary<string, object?> settings = [];

            foreach (PropertyInfo p in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object? raw = p.PropertyType.IsValueType ? Activator.CreateInstance(p.PropertyType) : null;

                if (p.CanRead)
                    raw = p.GetValue(Activator.CreateInstance(configType)!);

                bool isSecure = p.PropertyType == typeof(SecureValue) || p.GetCustomAttribute<SecureAttribute>() != null;

                settings[p.Name] = isSecure ? SecureValue.From(raw, context).EncryptedBase64 : raw;
            }

            string json = JsonSerializer.Serialize(settings, this.jsonOptions);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            Atomic.Write(filePath, json);
        }
    }
}