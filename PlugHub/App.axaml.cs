using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Accessors.Configuration;
using PlugHub.Models;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Shared;
using PlugHub.Shared.Interfaces;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.ViewModels;
using PlugHub.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PlugHub;


public partial class App : Application
{
    private static readonly TokenSet tokenSet =
        new(Token.New(), Token.Public, Token.Blocked);

    private IServiceProvider? serviceProvider;
    private IEnumerable<Plugin>? enabled;


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        this.serviceProvider = this.BuildServices();
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (this.serviceProvider == null)
        {
            return;
        }

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


    private IServiceProvider BuildServices()
    {
        IServiceCollection services = new ServiceCollection();

        IConfigService configService;

        services.AddLogging(builder =>
        {
            string temp = Path.GetTempPath();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(temp, $"plughub-{Environment.ProcessId}.log"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecureStorage, InsecureStorage>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IPluginResolver, PluginResolver>();
        services.AddSingleton<IPluginService, PluginService>();

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

            AppConfig appConfig = new()
            {
                // set default location (same folder as the .exe) but also allow overrides by env
                ConfigDirectory = AppContext.BaseDirectory
            };
            envConfig.Bind(appConfig);

            ConfigService configService =
                new(configProviders,
                    configAccessors,
                    logger,
                    tokenService,
                    AppContext.BaseDirectory,
                    appConfig.ConfigDirectory,
                    appConfig.ConfigJsonOptions);

            configService.RegisterConfig(
                typeof(AppConfig),
                new FileConfigServiceParams(
                    Owner: tokenSet.Owner,
                    Read: tokenSet.Read,
                    Write: tokenSet.Write));

            return configService;
        });
        services.AddSingleton<IPluginRegistrar>(provider =>
        {
            ILogger<IPluginRegistrar> logger = provider.GetRequiredService<ILogger<IPluginRegistrar>>();
            ITokenService tokenSearvice = provider.GetRequiredService<ITokenService>();
            IConfigService configService = provider.GetRequiredService<IConfigService>();

            FileConfigAccessorFor<PluginManifest> pluginManifest = new(tokenSearvice, configService, tokenSet);
            
            return new PluginRegistrar(logger, pluginManifest);
        });

        using (ServiceProvider injectorProvider = services.BuildServiceProvider())
        {
            ILogger<IPluginRegistrar> logger = injectorProvider.GetRequiredService<ILogger<IPluginRegistrar>>();
            IPluginService pluginService = injectorProvider.GetRequiredService<IPluginService>();
            IPluginResolver pluginSorter = injectorProvider.GetRequiredService<IPluginResolver>();
            configService = injectorProvider.GetRequiredService<IConfigService>();

            IEnumerable<Plugin> plugins =
                pluginService.Discover(
                    configService.GetSetting<string>(typeof(AppConfig), "PluginFolderPath", tokenSet));

            configService.RegisterConfig(
                new FileConfigServiceParams(Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<PluginManifest>? pluginConfig);

            PluginRegistrar.SynchronizePluginConfig(logger, pluginConfig, plugins);

            this.enabled = PluginRegistrar.GetEnabledInterfaces(logger, pluginConfig, plugins);

            PluginRegistrar.RegisterInjectors(logger, pluginService, pluginSorter, services, this.enabled);
        }
        using (ServiceProvider pluginProvider = services.BuildServiceProvider())
        {
            ILogger<PluginRegistrar> logger = pluginProvider.GetRequiredService<ILogger<PluginRegistrar>>();

            PluginRegistrar.RegisterPlugins(logger, services, this.enabled);
        }

        IServiceProvider provider = services.BuildServiceProvider();

        configService = provider.GetRequiredService<IConfigService>();
        configService.RegisterConfig(typeof(PluginManifest),
            new FileConfigServiceParams(
                Owner: tokenSet.Owner,
                Read: tokenSet.Read,
                Write: tokenSet.Write));

        BrandingPlugins(provider);

        ConfigurationPlugins(provider);

        return provider;
    }


    private static void BrandingPlugins(IServiceProvider provider)
    {
        IConfigService configService = provider.GetRequiredService<IConfigService>();
        IConfigAccessorFor<AppConfig> accessor = configService.GetAccessor<AppConfig>(tokenSet);
        IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
        IEnumerable<IPluginBranding> brandingPlugins = provider.GetServices<IPluginBranding>();

        List<PluginBrandingDescriptor> allDescriptors = [];

        string configPath = accessor.Get().ConfigDirectory;

        foreach (IPluginBranding brandingPlugin in brandingPlugins)
        {
            IEnumerable<PluginBrandingDescriptor> descriptors = brandingPlugin.GetBrandingDescriptors();

            allDescriptors.AddRange(descriptors);
        }

        PluginBrandingDescriptor[] reverseSortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors).Reverse()];

        foreach (PluginBrandingDescriptor descriptor in reverseSortedDescriptors)
        {
            try
            {
                descriptor.BrandConfiguration?.Invoke(accessor);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply configuration branding for plugin {PluginID}", descriptor.PluginID);
            }
        }

        foreach (PluginBrandingDescriptor descriptor in reverseSortedDescriptors)
        {
            try
            {
                descriptor.BrandServices?.Invoke(provider);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply service branding for plugin {PluginID}", descriptor.PluginID);
            }
        }

        string baseDir = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string currentConfigDir = Path.GetFullPath(configPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (currentConfigDir.Equals(baseDir,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            string appConfigPath = Path.Combine(baseDir, "AppConfig.json");

            if (!File.Exists(appConfigPath))
            {
                AppConfig appConfigBase = new();
                accessor.Set<string>(nameof(appConfigBase.ConfigDirectory), appConfigBase.ConfigDirectory);
                accessor.Save();
            }
        }
        else if (configPath != accessor.Get().ConfigDirectory)
        {
            Directory.CreateDirectory(accessor.Get().ConfigDirectory);

            configService.UnregisterConfig(typeof(AppConfig), tokenSet);
            configService.RegisterConfig(typeof(AppConfig),
                new FileConfigServiceParams(
                    Owner: tokenSet.Owner,
                    Read: tokenSet.Read,
                    Write: tokenSet.Write));
        }

        ConfigureSystemLogs(provider);
        ConfigureStorageLocation(provider);
    }
    private static void ConfigureSystemLogs(IServiceProvider provider)
    {
        IConfigService configService = provider.GetRequiredService<IConfigService>();
        IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(tokenSet);
        AppConfig appConfig = configAccessor.Get();

        RollingInterval rolloverInterval = (RollingInterval)appConfig.LoggingRolloverInterval;

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
    private static void ConfigureStorageLocation(IServiceProvider provider)
    {
        IConfigService configService = provider.GetRequiredService<IConfigService>();
        IConfigAccessorFor<AppConfig> configAccessor = configService.GetAccessor<AppConfig>(tokenSet);
        ISecureStorage secureStorage = provider.GetRequiredService<ISecureStorage>();

        secureStorage.Initialize(configAccessor.Get().StorageFolderPath);
    }

    private static void ConfigurationPlugins(IServiceProvider provider)
    {
        ITokenService tokenService = provider.GetRequiredService<ITokenService>();
        IConfigService configService = provider.GetRequiredService<IConfigService>();
        IEnumerable<IPluginConfiguration> configurationPlugins = provider.GetServices<IPluginConfiguration>();

        foreach (IPluginConfiguration configurationPlugin in configurationPlugins)
        {
            IEnumerable<PluginConfigurationDescriptor> descriptors =
                configurationPlugin.GetConfigurationDescriptors(tokenService);

            foreach (PluginConfigurationDescriptor descriptor in descriptors)
                configService.RegisterConfig(descriptor.ConfigType, descriptor.ConfigServiceParams);
        }
    }


    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Dispose DI container and all services
        (this.serviceProvider as IDisposable)?.Dispose();

        // Flush and dispose Serilog
        Serilog.Log.CloseAndFlush();
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