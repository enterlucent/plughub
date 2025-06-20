using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Services
{
    public class ConfigServiceConfig(IConfiguration baseConfig, IConfiguration userConfig, Token? read = null, Token? write = null)
    {
        public IConfiguration BaseConfig = baseConfig;
        public IConfiguration UserConfig = userConfig;
        public Token Read = read ?? Token.Public;
        public Token Write = write ?? read ?? Token.Public;
    }
    public class ConfigServiceSettings(Dictionary<string, object> baseSettings, Dictionary<string, object> userSettings, Token? read = null, Token? write = null)
    {
        public Dictionary<string, object> BaseSettings = baseSettings;
        public Dictionary<string, object> UserSettings = userSettings;
        public Token Read = read ?? Token.Public;
        public Token Write = write ?? read ?? Token.Public;
    }


    public class ConfigService : IConfigService
    {
        public event EventHandler<EventArgs>? AppConfigReloaded;
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;
        public event EventHandler<ConfigServiceAppSettingChangeEventArgs>? AppSettingChanged;
        public event EventHandler<ConfigServiceSettingChangeEventArgs>? SettingChanged;

        private readonly ILogger<ConfigService> logger;
        private readonly ITokenService tokenService;

        private readonly IConfiguration appConfig;
        private readonly Dictionary<Type, ConfigServiceConfig> configs = [];
        private readonly Dictionary<string, object> appSettings = [];
        private readonly Dictionary<Type, ConfigServiceSettings> settings = [];

        private readonly JsonSerializerOptions jsonOptions;
        private const string configFolderPath = "Config";
        private readonly string configRootDirectory;
        private readonly string configUserDirectory;

        private static readonly object iolock = new();
        private readonly SemaphoreSlim aiolock = new SemaphoreSlim(1, 1);

        public ConfigService(ILogger<ConfigService> logger, ITokenService tokenService, string configRootDirectory, string configUserDirectory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.tokenService = tokenService;

            string appSettingsFilePath = Path.Combine(configRootDirectory, "appsettings.json");
            string appSettingsDevelopmentFilePath = Path.Combine(configRootDirectory, "appsettings.Development.json");

            this.EnsureFileExists(appSettingsFilePath, optional: false);
            this.EnsureFileExists(appSettingsDevelopmentFilePath, optional: true);

            IConfigurationBuilder confBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(appSettingsFilePath, optional: false, reloadOnChange: true)
                .AddJsonFile(appSettingsDevelopmentFilePath, optional: true, reloadOnChange: true);

            this.appConfig = confBuilder.Build();
            this.appConfig.GetReloadToken().RegisterChangeCallback(this.OnAppConfigChanged, null);

            this.configRootDirectory = configRootDirectory;
            this.configUserDirectory = configUserDirectory;

            this.jsonOptions = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.WriteAsString,
            };

            this.LoadAppSettings();
        }


        public IConfigAccessor CreateAccessor(Type configTypes, Token? readToken = null, Token? writeToken = null)
        {
            Token secRead = readToken ?? Token.Public;
            Token secWrite = writeToken ?? secRead;

            this.RegisterConfig(configTypes, secRead, secWrite);

            return new ConfigAccessor(this, [configTypes], secRead, secWrite);
        }
        public IConfigAccessor CreateAccessor(IEnumerable<Type> configTypes, Token? readToken = null, Token? writeToken = null)
        {
            Token secRead = readToken ?? Token.Public;
            Token secWrite = writeToken ?? secRead;

            this.RegisterConfigs(configTypes, secRead, secWrite);

            return new ConfigAccessor(this, configTypes, secRead, secWrite);
        }


        public void RegisterConfig(Type configType, Token? readToken = null, Token? writeToken = null)
        {
            Token secRead = readToken ?? Token.Public;
            Token secWrite = writeToken ?? secRead;

            bool configExists = this.settings.TryGetValue(configType, out ConfigServiceSettings? existingSettings);

            if (configExists)
            {
                if (existingSettings != null)
                    if (!this.tokenService.ValidateAccessor(existingSettings.Write, secWrite, false))
                        throw new UnauthorizedAccessException(
                            $"Write token invalid for existing config: {configType.Name}");
            }
            else
            {
                // No existing config - only validate if token isn't blocked
                if (secWrite == Token.Blocked)
                    throw new UnauthorizedAccessException(
                        $"Blocked token cannot register new config: {configType.Name}");
            }

            string pluginName = configType.Name;
            string baseConfigFilePath = this.GetSettingsPath(configType, true);
            string userConfigFilePath = this.GetSettingsPath(configType, false);

            this.EnsureFileExists(baseConfigFilePath, false, configType);
            this.EnsureFileExists(userConfigFilePath, false);

            IConfiguration baseConfig = BuildConfig(baseConfigFilePath);
            IConfiguration userConfig = BuildConfig(userConfigFilePath);

            this.configs[configType] = new(baseConfig, userConfig, secRead, secWrite);

            Dictionary<string, object> baseSettings = [];
            Dictionary<string, object> userSettings = [];

            try
            {
                LoadSettings(baseConfig, baseSettings);
                LoadSettings(userConfig, userSettings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading settings for plugin '{pluginName}'", ex);
            }

            this.settings[configType] = new(baseSettings, userSettings, secRead, secWrite);

            baseConfig.GetReloadToken().RegisterChangeCallback(this.OnConfigChanged, configType);
            userConfig.GetReloadToken().RegisterChangeCallback(this.OnConfigChanged, configType);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, Token? readToken = null, Token? writeToken = null)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            lock (iolock)
            {
                foreach (Type configType in configTypes)
                    this.RegisterConfig(configType, readToken, writeToken);
            }
        }

        public IConfiguration GetEnvConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs())
                .Build();
        }

        public T GetAppSetting<T>(string key)
        {
            if (this.appSettings.TryGetValue(key, out object? value))
            {
                // Handle null for reference types
                if (value == null && default(T) == null)
                {
                    return (T)value!;
                }

                if (value is T typedValue)
                {
                    return typedValue;
                }
                else
                {
                    throw new InvalidCastException($"The value for key '{key}' cannot be cast to type '{typeof(T).FullName}'.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Application setting with key '{key}' not found.");
            }
        }
        public T GetSetting<T>(Type configType, string key, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            if (this.settings.TryGetValue(configType, out ConfigServiceSettings? settings))
            {
                this.tokenService.ValidateAccessor(settings.Read, secToken);

                if (settings.UserSettings.TryGetValue(key, out object? userValue))
                {
                    return (T)Convert.ChangeType(userValue, typeof(T));
                }

                if (settings.BaseSettings.TryGetValue(key, out object? baseValue))
                {
                    return (T)Convert.ChangeType(baseValue, typeof(T));
                }

                throw new KeyNotFoundException($"Setting '{key}' not found in plugin configurations for type '{configType.Name}'.");
            }
            else
            {
                throw new KeyNotFoundException($"Plugin configuration for type '{configType.Name}' is not registered.");
            }
        }

        public void SetAppSetting<T>(string key, T newValue)
        {
            lock (iolock)
            {
                object? oldValue = null;

                if (this.appSettings.TryGetValue(key, out object? value))
                {
                    oldValue = value;

                    try
                    {
                        T existingValue = value is T tValue
                            ? tValue
                            : (T)Convert.ChangeType(value, typeof(T));
                        if (EqualityComparer<T>.Default.Equals(existingValue, newValue))
                        {
                            return;
                        }
                    }
                    catch
                    {
                        // Ignore conversion errors, proceed to set new value
                    }
                }

                this.appSettings[key] = newValue!;

                AppSettingChanged?.Invoke(this,
                    new ConfigServiceAppSettingChangeEventArgs(key, oldValue, newValue));
            }
        }
        public void SetSetting<T>(Type configType, string key, T newValue, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            lock (iolock)
            {
                if (!this.settings.TryGetValue(configType, out ConfigServiceSettings? settings))
                {
                    throw new InvalidOperationException("Plugin configuration not registered.");
                }

                this.tokenService.ValidateAccessor(settings.Write, secToken);
                settings.UserSettings.TryGetValue(key, out object? oldValue);
                object? baseValue = settings.BaseSettings.TryGetValue(key, out object? sysValue) ? sysValue : default;

                if (oldValue == null && baseValue != null)
                    oldValue = baseValue;

                T? convertedSystemValue = default;
                bool canCompare = false;
                try
                {
                    if (baseValue != null)
                    {
                        convertedSystemValue = baseValue is T val ? val : (T)Convert.ChangeType(baseValue, typeof(T));

                        canCompare = true;
                    }
                }
                catch
                {
                    // Ignore conversion errors
                }

                if (canCompare && EqualityComparer<T>.Default.Equals(newValue, convertedSystemValue))
                    settings.UserSettings.Remove(key);
                else
                    settings.UserSettings[key] = newValue!;

                SettingChanged?.Invoke(this, new ConfigServiceSettingChangeEventArgs(configType, key, oldValue, newValue));
            }
        }

        public async Task SaveAppSettingsAsync()
        {
            string appSettingsFilePath = Path.Combine(this.configRootDirectory, "appsettings.json");
            try
            {
                await this.SaveToFileAsync(appSettingsFilePath, this.appSettings);
            }
            catch (Exception ex)
            {
                throw new IOException("Failed to save application settings.", ex);
            }
        }
        public async Task SaveSettingsAsync(Type configType, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            await aiolock.WaitAsync();
            try
            {
                if (!this.settings.TryGetValue(configType, out ConfigServiceSettings? settings))
                    throw new InvalidOperationException("Plugin configuration not registered.");

                this.tokenService.ValidateAccessor(settings.Write, secToken);

                string baseSettingsFilePath = this.GetSettingsPath(configType, true);
                string userSettingsFilePath = this.GetSettingsPath(configType, false);

                await this.SaveToFileAsync(baseSettingsFilePath, settings.BaseSettings);
                await this.SaveToFileAsync(userSettingsFilePath, settings.UserSettings);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save settings for '{configType.Name}'.", ex);
            }
            finally
            {
                aiolock.Release();
            }
        }

        public string GetBaseConfigFileContents(Type configType, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            if (this.settings.TryGetValue(configType, out ConfigServiceSettings? settings))
                this.tokenService.ValidateAccessor(settings.Read, secToken);

            string settingPath = this.GetSettingsPath(configType, true);

            if (!File.Exists(settingPath))
                throw new FileNotFoundException($"Base config file not found: {settingPath}");

            return File.ReadAllText(settingPath);
        }
        public string GetBaseConfigFileContents<TConfig>(Token? token = null) where TConfig : class
            => this.GetBaseConfigFileContents(typeof(TConfig), token);

        public async Task SaveBaseConfigFileContentsAsync(Type configType, string contents, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            if (this.settings.TryGetValue(configType, out ConfigServiceSettings? settings))
                this.tokenService.ValidateAccessor(settings.Write, secToken);

            string settingPath = this.GetSettingsPath(configType, true);

            try
            {
                if (!File.Exists(settingPath))
                {
                    string? settingDir = Path.GetDirectoryName(settingPath);

                    if (settingDir != null)
                        Directory.CreateDirectory(settingDir);
                }

                JsonDocument.Parse(contents);

                string tempPath = System.IO.Path.GetTempFileName();
                await File.WriteAllTextAsync(tempPath, contents);
                File.Move(tempPath, settingPath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new IOException($"An I/O error occurred while trying to write to the file '{settingPath}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access to the file '{settingPath}' is denied. Please check your permissions.", ex);
            }
            catch (JsonException ex)
            {
                throw new UnauthorizedAccessException($"Provided content failed to parse as JSON. Please check your contents.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"An unexpected error occurred while saving the file '{settingPath}'.", ex);
            }
        }
        public async Task SaveBaseConfigFileContentsAsync<TConfig>(string contents, Token? token = null) where TConfig : class
            => await this.SaveBaseConfigFileContentsAsync(typeof(TConfig), contents, token);

        public object GetConfigInstance(Type configType, Token? token = null)
        {
            Token secToken = token ?? Token.Public;

            if (!this.settings.TryGetValue(configType, out ConfigServiceSettings? settingsEntry))
                throw new KeyNotFoundException($"Configuration for type {configType.Name} not registered");

            this.tokenService.ValidateAccessor(settingsEntry.Read, secToken);

            string baseJson = this.GetBaseConfigFileContents(configType, token);

            try
            {
                if (JsonSerializer.Deserialize(baseJson, configType, this.jsonOptions) is { } result)
                    return result;

                return Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");
            }
            catch (JsonException ex)
            {
                this.logger.LogError(ex, $"Failed to deserialize {configType.Name}");
                return Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");
            }
        }
        public void SaveConfigInstance(Type configType, object updatedConfig, Token? token = null)
        {
            if (updatedConfig == null)
                throw new ArgumentNullException(nameof(updatedConfig));

            foreach (var prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                this.SetSetting(
                    configType,
                    prop.Name,
                    prop.GetValue(updatedConfig),
                    token
                );
            }
        }

        private void OnAppConfigChanged(object? state)
        {
            lock (iolock)
            {
                this.logger.LogInformation("App configuration has been reloaded.");

                try
                {
                    this.LoadAppSettings();

                    AppConfigReloaded?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    string messaage = $"Failed to reload application configuration";
                    this.logger.LogError(ex, messaage);
                    throw new InvalidOperationException(messaage, ex);
                }
                finally
                {
                    this.appConfig.GetReloadToken().RegisterChangeCallback(this.OnAppConfigChanged, null);
                }
            }
        }
        private void OnConfigChanged(object? state)
        {
            if (state == null) return;

            Type configType = (Type)state;

            lock (iolock)
            {
                this.logger.LogInformation("Plugin configuration for {PluginName} has been reloaded.", configType.Name);

                if (this.configs.TryGetValue(configType, out ConfigServiceConfig? configs))
                {
                    Dictionary<string, object> baseSettings = [];
                    Dictionary<string, object> userSettings = [];
                    bool reloadSuccessful = false;

                    try
                    {
                        LoadSettings(configs.BaseConfig, baseSettings);
                        LoadSettings(configs.UserConfig, userSettings);
                        reloadSuccessful = true;
                    }
                    catch (Exception ex)
                    {
                        string message = $"Error reloading settings for plugin {configType.Name}";
                        this.logger.LogError(ex, message);
                        throw new InvalidOperationException(message, ex);
                    }
                    finally
                    {
                        configs.BaseConfig.GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigChanged, configType);

                        configs.UserConfig.GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigChanged, configType);
                    }

                    if (reloadSuccessful)
                    {
                        this.settings[configType] =
                            new(baseSettings,
                                userSettings,
                                this.settings[configType].Read,
                                this.settings[configType].Write);

                        ConfigReloaded?.Invoke(this, new ConfigServiceConfigReloadedEventArgs(configType));
                    }
                }
                else
                {
                    string message = $"Plugin configuration for type '{configType.Name}' is not registered.";
                    this.logger.LogWarning(message);
                    throw new KeyNotFoundException(message);
                }
            }
        }

        private void EnsureFileExists(string filePath, bool optional = false, Type? configType = null)
        {
            string? directory = System.IO.Path.GetDirectoryName(filePath);

            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(filePath))
            {
                if (!optional)
                {
                    try
                    {
                        string content = "{}";

                        if (configType != null)
                            content = configType.SerializeToJson(this.jsonOptions);

                        File.WriteAllText(filePath, content);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Failed to create the required configuration file at '{filePath}'.", ex);
                    }
                }
                else
                {
                    // Optional file does not exist, no action needed
                }
            }
        }
        private static IConfiguration BuildConfig(string filePath)
        {
            try
            {
                return new ConfigurationBuilder()
                    .AddJsonFile(filePath, optional: true, reloadOnChange: true)
                    .Build();
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to build configuration from file '{filePath}'.", ex);
            }
        }

        private void LoadAppSettings()
        {
            lock (iolock)
            {
                this.appSettings.Clear();
                try
                {
                    LoadSettings(this.appConfig, this.appSettings);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to load application settings from configuration.", ex);
                }
            }
        }
        private static void LoadSettings(IConfiguration config, Dictionary<string, object> settings)
        {
            try
            {
                foreach (IConfigurationSection section in config.GetChildren())
                {
                    settings[section.Key] = section.Value!;

                    LoadSettings(section, settings);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load settings from configuration.", ex);
            }
        }

        private string GetSettingsPath(Type configType, bool baseFile)
        {
            if (baseFile)
                return Path.Combine(this.configRootDirectory, configFolderPath, $"{configType.Name}.BaseSettings.json");
            else
                return Path.Combine(this.configUserDirectory, configFolderPath, $"{configType.Name}.UserSettings.json");
        }
        private async Task SaveToFileAsync(string filePath, Dictionary<string, object> settings)
        {
            try
            {
                string tempPath = System.IO.Path.GetTempFileName();
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(settings, this.jsonOptions));
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new IOException($"An I/O error occurred while trying to write to the file '{filePath}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access to the file '{filePath}' is denied. Please check your permissions.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"An error occurred during JSON serialization for the file '{filePath}'.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"An unexpected error occurred while saving the file '{filePath}'.", ex);
            }
        }
    }
}
