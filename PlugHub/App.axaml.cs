using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Accessors.Configuration;
using PlugHub.Bootstrap;
using PlugHub.Models;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Services.Configuration.Providers;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration.Parameters;
using PlugHub.Shared.Utility;
using PlugHub.ViewModels;
using PlugHub.ViewModels.Pages;
using PlugHub.Views;
using PlugHub.Views.Windows;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlugHub
{
    public partial class App : Application
    {
        private static IServiceProvider? serviceProvider;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            ServiceCollection services = new();
            AppConfig appConfig = new();
            AppEnv appEnv = new();
            TokenSet tokenSet = new(Token.New(), Token.Public, Token.Blocked);

            services.AddLogging(builder =>
            {
                string temp = Path.GetTempPath();

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(Path.Combine(temp, $"plughub.log"), rollingInterval: RollingInterval.Infinite)
                    .CreateLogger();

                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });

            CollectServices(services);
            CollectViewModels(services);

            IConfigService configService = ConfigService.GetInstance(services, appConfig);

            AppConfig baseConfig = GetBaseAppConfig(configService, appConfig, tokenSet);
            AppEnv baseEnv = GetBaseAppEnv(configService, appEnv, tokenSet);

            configService.GetEnvConfig().Bind(baseConfig);

            serviceProvider = Bootstrapper.BuildEnv(services, configService, tokenSet, baseConfig, baseEnv);

            configService = serviceProvider.GetRequiredService<IConfigService>();

            ConfigureSystemLogs(configService, tokenSet);
            ConfigureStorageLocation(serviceProvider, configService, tokenSet);
            ConfigureTheme(configService, tokenSet, this.Styles, this.Resources);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (serviceProvider == null)
            {
                return;
            }

            if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                desktop.ShutdownRequested += OnShutdownRequested;
                desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
            }
            else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                //TODO: This needs to be integrated with the new MainView selection code
                //        which means it will need ot be extracted into something reusable
                singleViewPlatform.MainView = serviceProvider.GetRequiredService<MainView>();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            (serviceProvider as IDisposable)?.Dispose();

            Log.CloseAndFlush();
        }
        private static void DisableAvaloniaDataAnnotationValidation()
        {
            DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
                [.. BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>()];

            foreach (DataAnnotationsValidationPlugin? plugin in dataValidationPluginsToRemove)
                BindingPlugins.DataValidators.Remove(plugin);
        }

        #region App: Core Services & ViewModels

        private static void CollectServices(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ITokenService, TokenService>();
            services.AddSingleton<ISecureStorage, InsecureStorage>();
            services.AddSingleton<IEncryptionService, EncryptionService>();

            services.AddSingleton<IConfigProvider, FileConfigProvider>();
            services.AddTransient<IConfigAccessor, FileConfigAccessor>();
        }
        private static void CollectViewModels(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<MainView>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SettingsView>();
            services.AddSingleton<SettingsViewModel>();

            services.AddSingleton<MainWindow>();
        }

        #endregion

        #region App: Post-Init Configuration

        private static void ConfigureSystemLogs(IConfigService configService, TokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);

            IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(tokenSet);
            AppConfig appConfig = configAccessor.Get();

            RollingInterval rolloverInterval = (RollingInterval)(appConfig.LoggingRolloverInterval ?? LoggingRollingInterval.Day);

            string? defaultFileName = new AppConfig().LoggingFileName;

            if (string.IsNullOrWhiteSpace(defaultFileName))
            {
                Log.Warning("[App] Default log file name is missing or empty. Exiting log configuration.");

                return;
            }

            string? logFileName = appConfig.LoggingFileName;

            if (string.IsNullOrWhiteSpace(logFileName))
            {
                Log.Warning("[App] Runtime log file name is missing or empty. Exiting log configuration.");

                return;
            }

            string? defaultDir = new AppConfig().LoggingDirectory;

            if (string.IsNullOrWhiteSpace(defaultDir))
            {
                Log.Warning("[App] Default log directory is missing or empty. Exiting log configuration.");

                return;
            }

            string? runtimeDir = appConfig.LoggingDirectory;

            if (string.IsNullOrWhiteSpace(runtimeDir))
            {
                Log.Warning("[App] Runtime log directory is missing or empty. Exiting log configuration.");

                return;
            }

            string configLogPath =
                Path.GetFullPath(Path.Combine(runtimeDir, logFileName))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string standardLogPath =
                Path.GetFullPath(Path.Combine(defaultDir, defaultFileName))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!PlatformPath.Equals(configLogPath, standardLogPath))
            {
                Log.CloseAndFlush();

                Directory.CreateDirectory(runtimeDir);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(configLogPath, rollingInterval: rolloverInterval)
                    .CreateLogger();
            }
        }
        private static void ConfigureStorageLocation(IServiceProvider provider, IConfigService configService, TokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);

            IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(tokenSet);
            ISecureStorage secureStorage = provider.GetRequiredService<ISecureStorage>();

            string? storageFolder = configAccessor.Get().StorageDirectory;

            if (storageFolder != null)
                secureStorage.Initialize(storageFolder);
        }
        private static void ConfigureTheme(IConfigService configService, TokenSet tokenSet, Styles styles, IResourceDictionary resources)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);

            IEnumerable<IPluginStyleInclusion> styleIncludeProviders = serviceProvider.GetServices<IPluginStyleInclusion>();
            ILogger<App> logger = serviceProvider.GetRequiredService<ILogger<App>>();
            IPluginResolver pluginResolver = serviceProvider.GetRequiredService<IPluginResolver>();

            IReadOnlyList<PluginStyleIncludeDescriptor> orderedDescriptors =
                pluginResolver.ResolveAndOrder<IPluginStyleInclusion, PluginStyleIncludeDescriptor>(styleIncludeProviders);

            HashSet<string> loadedResourceDictionaries = [];
            HashSet<Type> loadedFactoryTypes = [];

            IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(owner: tokenSet.Owner);
            IConfigAccessorFor<AppEnv> envAccessor = configService.GetAccessor<AppEnv>(owner: tokenSet.Owner);

            AppConfig appConfig = configAccessor.Get();
            AppEnv appEnv = envAccessor.Get();

            bool useDefaultTheme = appEnv.UseDefaultTheme ?? appConfig.UseDefaultTheme;
            bool preferSystemTheme = appEnv.PreferSystemTheme ?? appConfig.PreferSystemTheme;
            bool preferUserAccentColor = appEnv.PreferUserAccentColor ?? appConfig.PreferUserAccentColor;

            string systemThemeStr =
                !string.IsNullOrWhiteSpace(appEnv.SystemTheme)
                    ? appEnv.SystemTheme!
                    : appConfig.SystemTheme;

            if (!useDefaultTheme)
                return;

            ThemeVariant requested = systemThemeStr?.Trim().ToLowerInvariant() switch
            {
                "light" => ThemeVariant.Light,
                "dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = requested;

            styles.Add(new StyleInclude(new Uri("avares://PlugHub/"))
            {
                Source = new Uri("avares://PlugHub/Styles/Icons.axaml")
            });

            styles.Add(new FluentAvaloniaTheme
            {
                PreferSystemTheme = preferSystemTheme,
                PreferUserAccentColor = preferUserAccentColor
            });

            resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://PlugHub/"))
            {
                Source = new Uri("avares://PlugHub/Styles/Generic.axaml")
            });


            foreach (PluginStyleIncludeDescriptor descriptor in orderedDescriptors)
            {
                if (Application.Current?.Styles is null)
                    continue;

                try
                {
                    if (descriptor.Factory is not null)
                    {
                        IStyle style = descriptor.Factory();

                        if (loadedFactoryTypes.Add(style.GetType()))
                            Application.Current.Styles.Add(style);
                        else
                            logger.LogDebug("[App] Skipped duplicate factory style of type {StyleType}", style.GetType().FullName);
                    }
                    else if (!string.IsNullOrEmpty(descriptor.ResourceUri) && loadedResourceDictionaries.Add(descriptor.ResourceUri))
                    {
                        Uri baseUri = string.IsNullOrEmpty(descriptor.BaseUri)
                            ? new Uri("avares://PlugHub/")
                            : new Uri(descriptor.BaseUri);

                        StyleInclude styleInclude = new(baseUri)
                        {
                            Source = new Uri(descriptor.ResourceUri)
                        };

                        Application.Current.Styles.Add(styleInclude);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[App] Failed to load style from {Source}", descriptor.ResourceUri ?? descriptor.Factory?.Method?.Name ?? "unknown");
                }
            }

            logger.LogInformation("[App] PluginsStyleIncludes completed: Added {ResourceCount} unique resource dictionaries and {FactoryCount} unique factory styles.", loadedResourceDictionaries.Count, loadedFactoryTypes.Count);
        }

        #endregion

        #region App: Configuration Service Initialization

        private static AppConfig GetBaseAppConfig(IConfigService configService, AppConfig config, TokenSet tokenSet)
        {
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "AppConfig.json");

            if (PlatformPath.Exists(configFilePath))
            {
                configService.RegisterConfig(
                    new ConfigFileParams(configFilePath, Owner: tokenSet.Owner),
                    out IConfigAccessorFor<AppConfig>? accessor);

                AppConfig? loadedConfig = accessor?.Get() ?? new AppConfig();

                foreach (PropertyInfo prop in typeof(AppConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        object? loadedValue = prop.GetValue(loadedConfig);

                        if (loadedValue != null)
                            prop.SetValue(config, loadedValue);
                    }
                }

                configService.UnregisterConfig(typeof(AppConfig), tokenSet);
            }
            return config;
        }
        private static AppEnv GetBaseAppEnv(IConfigService configService, AppEnv env, TokenSet tokenSet)
        {
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "AppEnv.json");

            if (PlatformPath.Exists(configFilePath))
            {
                configService.RegisterConfig(
                    new ConfigFileParams(configFilePath, Owner: tokenSet.Owner),
                    out IConfigAccessorFor<AppEnv>? accessor);

                AppEnv? loadedEnv = accessor?.Get() ?? new AppEnv();

                foreach (PropertyInfo prop in typeof(AppEnv).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        object? loadedValue = prop.GetValue(loadedEnv);

                        if (loadedValue != null)
                            prop.SetValue(env, loadedValue);
                    }
                }

                configService.UnregisterConfig(typeof(AppEnv), tokenSet);
            }

            return env;
        }

        #endregion
    }
}