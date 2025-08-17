using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace PlugHub.Services.Configuration.Providers
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
            {
                return new SecureValue(r.GetString()!);
            }

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

    public class SecureFileConfigService : FileConfigService, IConfigServiceProvider, IDisposable
    {
        protected IEncryptionService EncryptionService;

        public SecureFileConfigService(ILogger<IConfigServiceProvider> logger, ITokenService tokenService, IEncryptionService encryptionService)
            : base(logger, tokenService)
        {
            ArgumentNullException.ThrowIfNull(encryptionService);

            this.EncryptionService = encryptionService;
            this.SupportedParamsTypes = [typeof(SecureFileConfigServiceParams)];
            this.RequiredAccessorInterface = typeof(ISecureFileConfigAccessor);

            this.Logger.LogDebug("SecureFileConfigService initialized");
        }

        #region SecureFileConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(configService);

            if (configParams is not SecureFileConfigServiceParams)
            {
                throw new ArgumentException($"Expected SecureFileConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));
            }

            SecureFileConfigServiceParams p = (SecureFileConfigServiceParams)configParams;

            this.ValidateSecureFieldConfiguration(configType);
            JsonSerializerOptions jsonOptions = this.PrepareSecureJsonOptions(p, configService);
            string defaultConfigFilePath = this.ResolveSecureConfigPath(p, configService, configType);
            IEncryptionContext encryptionContext = this.GetOrCreateEncryptionContext(p, configType);

            this.EnsureEncryptedFileExists(defaultConfigFilePath, configType, encryptionContext, jsonOptions);
            base.RegisterConfig(configType, p, configService);

            this.Logger.LogDebug("Registered secure configuration: {ConfigType}", configType.Name);
        }

        #endregion

        #region SecureFileConfigService: Secure Value Handling

        [return: MaybeNull]
        protected override T CastStoredValue<T>(object? raw)
        {
            if (raw is null)
            {
                return default;
            }

            bool isCorrectType = raw is T;

            if (isCorrectType)
            {
                return (T)raw;
            }

            bool isSecureValueRequest = typeof(T) == typeof(SecureValue);
            bool rawIsSecureValue = raw is SecureValue;

            if (isSecureValueRequest && rawIsSecureValue)
            {
                return (T)raw;
            }

            if (rawIsSecureValue)
            {
                throw new InvalidCastException($"Setting contains a SecureValue but was requested as {typeof(T).Name}. Use a SecureConfigAccessor or request SecureValue directly.");
            }

            try
            {
                bool isConvertible = raw is IConvertible;

                if (isConvertible)
                {
                    return (T)Convert.ChangeType(raw, typeof(T));
                }
            }
            catch (InvalidCastException ex)
            {
                this.Logger.LogWarning(ex, "Invalid cast when converting stored value");
            }
            catch (FormatException ex)
            {
                this.Logger.LogWarning(ex, "Format error when converting stored value");
            }
            catch (OverflowException ex)
            {
                this.Logger.LogWarning(ex, "Overflow error when converting stored value");
            }

            return default;
        }

        protected override object? GetBuildSettingsValue(IConfiguration config, PropertyInfo prop)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(prop);

            bool isSecureProperty = IsSecureProperty(prop);

            if (isSecureProperty)
            {
                return ExtractSecureValue(config, prop);
            }
            else
            {
                return base.GetBuildSettingsValue(config, prop);
            }
        }

        #endregion

        #region SecureFileConfigService: Configuration Validation

        private void ValidateSecureFieldConfiguration(Type configType)
        {
            bool hasSecureFields = HasSecureFields(configType);

            if (!hasSecureFields)
            {
                this.Logger.LogInformation("Type '{ConfigType}' contains no SecureValue properties; for faster startup consider using RegisterConfig instead.", configType.Name);
            }
        }

        private static bool HasSecureFields(Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                bool isSecureProperty = IsSecureProperty(property);

                if (isSecureProperty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSecureProperty(PropertyInfo prop)
        {
            bool isSecureValueType = prop.PropertyType == typeof(SecureValue);
            bool hasSecureAttribute = prop.GetCustomAttribute<SecureAttribute>() != null;

            return isSecureValueType || hasSecureAttribute;
        }

        #endregion

        #region SecureFileConfigService: Configuration Setup

        private JsonSerializerOptions PrepareSecureJsonOptions(SecureFileConfigServiceParams p, IConfigService configService)
        {
            JsonSerializerOptions jsonOptions = p.JsonSerializerOptions ?? this.JsonOptions ?? configService.JsonOptions;
            jsonOptions.Converters.Add(new SecureValueJsonConverter());

            return jsonOptions;
        }

        private string ResolveSecureConfigPath(SecureFileConfigServiceParams p, IConfigService configService, Type configType)
        {
            return this.ResolveLocalFilePath(p.ConfigUriOverride, configService.ConfigDataDirectory, configType, "json");
        }

        private IEncryptionContext GetOrCreateEncryptionContext(SecureFileConfigServiceParams p, Type configType)
        {
            if (p.EncryptionContext != null)
            {
                return p.EncryptionContext;
            }
            else
            {
                Guid deterministicId = configType.ToDeterministicGuid();
                return this.EncryptionService.GetEncryptionContext(configType, deterministicId);
            }
        }

        #endregion

        #region SecureFileConfigService: File Operations

        private void EnsureEncryptedFileExists(string filePath, Type configType, IEncryptionContext context, JsonSerializerOptions jsonOptions)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(jsonOptions);

            bool fileExists = File.Exists(filePath);

            if (fileExists)
            {
                return;
            }

            this.EnsureDirectoryExists(filePath);

            Dictionary<string, object?> settings = this.BuildDefaultSecureSettings(configType, context);
            string jsonContent = SerializeSecureSettings(settings, jsonOptions);

            WriteSecureConfigFile(filePath, jsonContent);

            this.Logger.LogDebug("Created encrypted configuration file: {FilePath}", filePath);
        }

        private Dictionary<string, object?> BuildDefaultSecureSettings(Type configType, IEncryptionContext context)
        {
            Dictionary<string, object?> settings = [];
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                object? defaultValue = this.GetPropertyDefaultValue(property, configType);
                object? settingValue = PrepareSecureSettingValue(property, defaultValue, context);

                settings[property.Name] = settingValue;
            }

            return settings;
        }

        private object? GetPropertyDefaultValue(PropertyInfo property, Type configType)
        {
            object? rawValue = property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null;

            bool canReadProperty = property.CanRead;

            if (canReadProperty)
            {
                try
                {
                    object? instance = Activator.CreateInstance(configType);

                    rawValue = property.GetValue(instance);
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to read default value for property {PropertyName} on type {ConfigType}", property.Name, configType.Name);
                }
            }

            return rawValue;
        }

        private static object? PrepareSecureSettingValue(PropertyInfo property, object? rawValue, IEncryptionContext context)
        {
            bool isSecureProperty = IsSecureProperty(property);

            if (isSecureProperty)
            {
                SecureValue secureValue = SecureValue.From(rawValue, context);

                return secureValue.EncryptedBase64;
            }
            else
            {
                return rawValue;
            }
        }

        private static string SerializeSecureSettings(Dictionary<string, object?> settings, JsonSerializerOptions jsonOptions)
        {
            try
            {
                return JsonSerializer.Serialize(settings, jsonOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to serialize secure configuration settings", ex);
            }
        }

        private static void WriteSecureConfigFile(string filePath, string jsonContent)
        {
            try
            {
                string? directoryPath = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                Atomic.Write(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to write secure configuration file: {filePath}", ex);
            }
        }

        #endregion

        #region SecureFileConfigService: Secure Value Extraction

        private static SecureValue? ExtractSecureValue(IConfiguration config, PropertyInfo prop)
        {
            IConfigurationSection section = config.GetSection(prop.Name);

            bool sectionExists = section.Exists();

            if (!sectionExists)
            {
                return null;
            }

            string? base64Value = GetSecureValueFromSection(section);

            return base64Value is null ? null : new SecureValue(base64Value);
        }

        private static string? GetSecureValueFromSection(IConfigurationSection section)
        {
            string? directValue = section.Value;

            if (!string.IsNullOrEmpty(directValue))
            {
                return directValue;
            }

            string? base64Property = section["EncryptedBase64"];

            return base64Property;
        }

        #endregion
    }
}