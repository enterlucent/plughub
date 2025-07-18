﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlugHub.Services.Configuration
{
    public class SecureFileConfigServiceConfig(IEncryptionContext encryptionContext, IConfigService configService, string configPath, IConfiguration config, Dictionary<string, object?> values, JsonSerializerOptions? jsonOptions, Token ownerToken, Token readToken, Token writeToken, bool reloadOnChange)
        : FileConfigServiceConfig(configService, configPath, config, values, jsonOptions, ownerToken, readToken, writeToken, reloadOnChange)
    {
        public IEncryptionContext? EncryptionContext { get; init; } = encryptionContext;
    }

    public sealed class SecureValueJsonConverter : JsonConverter<SecureValue>
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

    public class SecureFileConfigService
        : FileConfigService, IConfigServiceProvider, IDisposable
    {
        protected IEncryptionService EncryptionService;

        public SecureFileConfigService(ILogger<IConfigServiceProvider> logger, ITokenService tokenService, IEncryptionService encryptionService)
            : base(logger, tokenService)
        {
            this.EncryptionService = encryptionService;
            this.SupportedParamsTypes = [typeof(SecureFileConfigServiceParams)];
            this.RequiredAccessorInterface = typeof(ISecureFileConfigAccessor);
        }

        #region SecureConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            if (configParams is not SecureFileConfigServiceParams p)
                throw new ArgumentException($"Expected UserConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));

            (Token nOwner, Token nRead, Token nWrite) = this.TokenService.CreateTokenSet(p.Owner, p.Read, p.Write);

            bool hasSecureFields = configType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.PropertyType == typeof(SecureValue) || p.GetCustomAttribute<SecureAttribute>() != null);

            if (!hasSecureFields)
            {
                this.Logger.LogInformation(
                    "Type '{ConfigType}' contains no SecureValue properties; for faster startup consider using RegisterConfig instead.",
                    configType.Name);
            }

            JsonSerializerOptions jsonOptions = p.JsonSerializerOptions ?? this.JsonOptions ?? configService.JsonOptions;
            jsonOptions.Converters.Add(new SecureValueJsonConverter());

            string defaultConfigFilePath =
                this.ResolveLocalFilePath(p.ConfigUriOverride, configService.ConfigDataDirectory, configType, "json");

            IEncryptionContext encryptionContext = p.EncryptionContext
                ?? this.EncryptionService.GetEncryptionContext(configType, configType.ToDeterministicGuid());

            this.EnsureEncryptedFileExists(defaultConfigFilePath, configType, encryptionContext, jsonOptions);

            base.RegisterConfig(configType, p, configService);
        }

        #endregion

        #region UserConfigService: Utility

        [return: MaybeNull]
        protected override T CastStoredValue<T>(object? raw)
        {
            if (raw is null) return default;

            if (raw is T typed) return typed;

            if (typeof(T) == typeof(SecureValue) && raw is SecureValue sv)
                return (T)(object)sv;

            if (raw is SecureValue)
                throw new InvalidCastException(
                    $"Setting contains a SecureValue but was requested as {typeof(T).Name}. " +
                    "Use a SecureConfigAccessor or request SecureValue directly.");

            try
            {
                if (raw is IConvertible)
                    return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch (InvalidCastException) { }
            catch (FormatException) { }
            catch (OverflowException) { }

            return default;
        }

        protected override object? GetBuildSettingsValue(IConfiguration config, PropertyInfo prop)
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

        private void EnsureEncryptedFileExists(string filePath, Type configType, IEncryptionContext context, JsonSerializerOptions jsonOptions)
        {
            if (File.Exists(filePath))
                return;

            this.EnsureDirectoryExists(filePath);

            Dictionary<string, object?> settings = [];

            foreach (PropertyInfo p in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object? raw = p.PropertyType.IsValueType ? Activator.CreateInstance(p.PropertyType) : null;

                if (p.CanRead)
                    raw = p.GetValue(Activator.CreateInstance(configType)!);

                bool isSecure = p.PropertyType == typeof(SecureValue) || p.GetCustomAttribute<SecureAttribute>() != null;

                settings[p.Name] = isSecure ? SecureValue.From(raw, context).EncryptedBase64 : raw;
            }

            string json = JsonSerializer.Serialize(settings, jsonOptions);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            Atomic.Write(filePath, json);
        }

        #endregion        
    }
}