using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Accessors;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.ViewModels;
using PlugHub.Views;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PlugHub;


internal sealed class AppConfig
{
    public string BaseDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub");

    public string ConfigDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Config");

    public JsonSerializerOptions JsonSerializationOptions { get; set; } = new JsonSerializerOptions();

    public bool HotReloadOnChange { get; set; } = false;
}


public partial class App : Application
{
    private IServiceProvider? serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        this.serviceProvider = BuildServices().BuildServiceProvider();
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

    private static IServiceCollection BuildServices()
    {
        IServiceCollection services = new ServiceCollection();

        BuildGlobalServices(services);

        return services;
    }

    private static void BuildGlobalServices(IServiceCollection services)
    {
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecureStorage, InsecureStorage>();

        services.AddTransient<IConfigAccessor, ConfigAccessor>();

        services.AddSingleton<IConfigService>(provider =>
        {
            ILogger<ConfigService> logger = new NullLogger<ConfigService>();
            ITokenService tokenService = provider.GetRequiredService<ITokenService>();

            string rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Config");
            string userDir = rootDir;

            ConfigService tempConfig = new(logger, tokenService, rootDir, userDir);

            IConfiguration envConfig = tempConfig.GetEnvConfig();
            AppConfig appConfig = new();
            envConfig.Bind(appConfig);

            ConfigService config = new(logger, tokenService, appConfig.ConfigDirectory, appConfig.ConfigDirectory);

            config.RegisterConfig(
                typeof(AppConfig),
                tokenService.CreateToken(),
                Token.Public,
                Token.Blocked,
                appConfig.JsonSerializationOptions,
                appConfig.HotReloadOnChange);

            return config;
        });
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