using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Accessors.Configuration;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.ViewModels;
using PlugHub.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PlugHub;



internal sealed class AppConfig
{
    public string BaseDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub");

    #region AppConfig: Logging Settings

    public string LoggingDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Logging");

    public RollingInterval LoggingRolloverInterval { get; init; } = RollingInterval.Day;

    public string LoggingFileName { get; init; } = "application-.log";

    #endregion

    #region AppConfig: Config Settings

    public string ConfigDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Config");

    public JsonSerializerOptions ConfigJsonOptions { get; set; } = new JsonSerializerOptions();

    public bool HotReloadOnChange { get; set; } = false;

    #endregion

    #region AppConfig: Local Storage

    public string StorageFolderPath { get; set; }
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Storage");

    #endregion
}


public partial class App : Application
{
    private IServiceProvider? serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        this.serviceProvider = BuildServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (this.serviceProvider == null)
            return;

        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktop.ShutdownRequested += this.OnShutdownRequested;

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Dispose DI container and all services
        (this.serviceProvider as IDisposable)?.Dispose();

        // Flush and dispose Serilog
        Serilog.Log.CloseAndFlush();
    }

    private static IServiceProvider BuildServices()
    {
        IServiceCollection services = new ServiceCollection();

        BuildGlobalServices(services);

        IServiceProvider provider = services.BuildServiceProvider();

        PostBuildConfiguration(provider);

        return provider;
    }
    private static void PostBuildConfiguration(IServiceProvider provider)
    {
        ConfigureBranding(provider);
        ConfigureSystemLogs(provider);
        ConfigureStorageLocation(provider);
    }

    private static void BuildGlobalServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            AppConfig appConfig = new();

            Directory.CreateDirectory(appConfig.LoggingDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(appConfig.LoggingDirectory, appConfig.LoggingFileName),
                    rollingInterval: appConfig.LoggingRolloverInterval)
                .CreateLogger();

            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecureStorage, InsecureStorage>();
        services.AddSingleton<IEncryptionService, EncryptionService>();

        services.AddSingleton<IConfigServiceProvider, FileConfigService>();
        services.AddSingleton<IConfigServiceProvider, SecureFileConfigService>();
        services.AddSingleton<IConfigServiceProvider, UserFileConfigService>();
        services.AddSingleton<IConfigServiceProvider, SecureUserFileConfigService>();

        services.AddTransient<IConfigAccessor, FileConfigAccessor>();
        services.AddTransient<IConfigAccessor, SecureFileConfigAccessor>();

        services.AddSingleton<IConfigService>(provider =>
        {
            IEnumerable<IConfigServiceProvider> configProviders = provider.GetRequiredService<IEnumerable<IConfigServiceProvider>>();
            IEnumerable<IConfigAccessor> configAccessors = provider.GetRequiredService<IEnumerable<IConfigAccessor>>();
            ILogger<IConfigService> logger = provider.GetRequiredService<ILogger<IConfigService>>();
            ITokenService tokenService = provider.GetRequiredService<ITokenService>();
            IConfiguration envConfig = ConfigService.GetEnvConfig();

            AppConfig appConfig = new();
            envConfig.Bind(appConfig);

            return new ConfigService(configProviders, configAccessors, logger, tokenService, AppContext.BaseDirectory, appConfig.ConfigDirectory, appConfig.ConfigJsonOptions);
        });
    }

    private static void ConfigureBranding(IServiceProvider provider)
    {
        IConfigService configService = provider.GetRequiredService<IConfigService>();
        ITokenService tokenService = provider.GetRequiredService<ITokenService>();
        ITokenSet tokenSet = tokenService.CreateTokenSet();

        configService.RegisterConfig(typeof(AppConfig), new FileConfigServiceParams(Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write));
    }
    private static void ConfigureSystemLogs(IServiceProvider provider)
    {
        string keyRollover = "LoggingRolloverInterval";
        string keyFileName = "LoggingFileName";
        string keyLogDir = "LoggingDirectory";

        IConfigService configService = provider.GetRequiredService<IConfigService>();

        RollingInterval rolloverInterval =
            configService.GetSetting<RollingInterval>(typeof(AppConfig), keyRollover, readToken: Token.Public);

        string? defaultFileName =
            configService.GetDefault<string>(typeof(AppConfig), keyFileName, readToken: Token.Public);

        if (string.IsNullOrWhiteSpace(defaultFileName))
        {
            Log.Warning("ConfigureSystemLogs: Default log file name is missing or empty. Exiting log configuration.");
            return;
        }

        string? logFileName =
            configService.GetSetting<string>(typeof(AppConfig), keyFileName, readToken: Token.Public);

        if (string.IsNullOrWhiteSpace(logFileName))
        {
            Log.Warning("ConfigureSystemLogs: Runtime log file name is missing or empty. Exiting log configuration.");
            return;
        }

        string? defaultDir = configService.GetDefault<string>(typeof(AppConfig), keyLogDir, readToken: Token.Public);

        if (string.IsNullOrWhiteSpace(defaultDir))
        {
            Log.Warning("ConfigureSystemLogs: Default log directory is missing or empty. Exiting log configuration.");
            return;
        }

        string standardLogPath
            = Path.GetFullPath(Path.Combine(defaultDir, defaultFileName))
                  .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string? runtimeDir = configService.GetSetting<string>(typeof(AppConfig), keyLogDir, readToken: Token.Public);

        if (string.IsNullOrWhiteSpace(runtimeDir))
        {
            Log.Warning("ConfigureSystemLogs: Runtime log directory is missing or empty. Exiting log configuration.");
            return;
        }

        string configLogPath
            = Path.GetFullPath(Path.Combine(runtimeDir, logFileName))
                  .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(configLogPath, standardLogPath, comparison))
        {
            Directory.CreateDirectory(runtimeDir);

            Log.Information("ConfigureSystemLogs: Log file location changed to {Path}", configLogPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(configLogPath, rollingInterval: rolloverInterval)
                .CreateLogger();

            Log.Information("ConfigureSystemLogs: Log file location changed from {Path}", standardLogPath);

            Log.CloseAndFlush();
        }
    }
    private static void ConfigureStorageLocation(IServiceProvider provider)
    {
        string keyStoragePath = "StorageFolderPath";

        IConfigService configService = provider.GetRequiredService<IConfigService>();
        ISecureStorage secureStorage = provider.GetRequiredService<ISecureStorage>();

        string storagePath = configService.GetSetting<string>(typeof(AppConfig), keyStoragePath, readToken: Token.Public);

        secureStorage.Initialize(storagePath);
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            [.. BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>()];

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin? plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}