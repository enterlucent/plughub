using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
using PlugHub.ViewModels;
using PlugHub.Views;
using PlugHub.Views.Windows;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PlugHub
{
    public partial class App : Application
    {
        private static IServiceProvider? serviceProvider;
        private static readonly ServiceCollection services = new();
        private static readonly AppConfig appConfig = new();
        private static readonly TokenSet tokenSet = new(Token.New(), Token.Public, Token.Blocked);

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            services.AddLogging(builder =>
            {
                string temp = Path.GetTempPath();

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(Path.Combine(temp, $"plughub-{Environment.ProcessId}.log"), rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: true);
            });

            CollectServices(services);
            CollectViewModels(services);

            IConfigService configService = ConfigService.GetInstance(services, appConfig);

            AppConfig baseConfig = GetBaseAppConfig(configService, appConfig, tokenSet);
            AppConfig userConfig = new();

            ConfigService.GetEnvConfig().Bind(baseConfig);

            (serviceProvider, userConfig) = Bootstrapper.BuildEnv(services, configService, tokenSet, baseConfig);

            ConfigureSystemLogs(configService, tokenSet);
            ConfigureStorageLocation(serviceProvider, configService, tokenSet);
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
                singleViewPlatform.MainView = serviceProvider.GetRequiredService<MainView>();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            (serviceProvider as IDisposable)?.Dispose();

            Serilog.Log.CloseAndFlush();
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

            services.AddSingleton<IConfigServiceProvider, FileConfigService>();
            services.AddTransient<IConfigAccessor, FileConfigAccessor>();
        }
        private static void CollectViewModels(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<MainView>();
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<MainWindow>();
        }

        #endregion

        #region App: System Logging and Storage Configuration

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
                Log.Warning("ConfigureSystemLogs: Default log file name is missing or empty. Exiting log configuration.");

                return;
            }

            string? logFileName = appConfig.LoggingFileName;

            if (string.IsNullOrWhiteSpace(logFileName))
            {
                Log.Warning("ConfigureSystemLogs: Runtime log file name is missing or empty. Exiting log configuration.");

                return;
            }

            string? defaultDir = new AppConfig().LoggingDirectory;

            if (string.IsNullOrWhiteSpace(defaultDir))
            {
                Log.Warning("ConfigureSystemLogs: Default log directory is missing or empty. Exiting log configuration.");

                return;
            }

            string standardLogPath
                = Path.GetFullPath(Path.Combine(defaultDir, defaultFileName))
                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string? runtimeDir = appConfig.LoggingDirectory;

            if (string.IsNullOrWhiteSpace(runtimeDir))
            {
                Log.Warning("ConfigureSystemLogs: Runtime log directory is missing or empty. Exiting log configuration.");
                return;
            }
            string configLogPath
                = Path.GetFullPath(Path.Combine(runtimeDir, logFileName))
                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            StringComparison comparison =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

            if (!string.Equals(configLogPath, standardLogPath, comparison))
            {
                Directory.CreateDirectory(runtimeDir);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(configLogPath, rollingInterval: rolloverInterval)
                    .CreateLogger();

                Log.CloseAndFlush();
            }
        }
        private static void ConfigureStorageLocation(IServiceProvider provider, IConfigService configService, TokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);

            IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(tokenSet);
            ISecureStorage secureStorage = provider.GetRequiredService<ISecureStorage>();

            string? storageFolder = configAccessor.Get().StorageFolderPath;

            if (storageFolder != null)
                secureStorage.Initialize(storageFolder);
        }

        #endregion

        #region App: Configuration Service Initialization

        private static AppConfig GetBaseAppConfig(IConfigService configService, AppConfig config, TokenSet tokenSet)
        {
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "AppConfig.json");

            if (PathUtilities.ExistsOsAware(configFilePath))
            {
                if (configService.IsConfigRegistered(typeof(AppConfig)))
                    configService.UnregisterConfig(typeof(AppConfig), tokenSet);

                configService.RegisterConfig(
                    new FileConfigServiceParams(configFilePath, Owner: tokenSet.Owner),
                    out IConfigAccessorFor<AppConfig>? accessor);

                AppConfig? loadedConfig = accessor?.Get() ?? new AppConfig();

                foreach (PropertyInfo prop in typeof(AppConfig).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        object? loadedValue = prop.GetValue(loadedConfig);

                        if (loadedValue != null)
                        {
                            prop.SetValue(config, loadedValue);
                        }
                    }
                }

                configService.UnregisterConfig(typeof(AppConfig), tokenSet);
            }
            return config;
        }

        #endregion
    }
}