using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Quartz.Application.Interfaces;
using Quartz.Application.Services;
using Quartz.Core.Interfaces;
using Quartz.Infrastructure.Parsers;
using Quartz.UI.ViewModels;
using Quartz.UI.Views;

namespace Quartz.UI;

public class App : Avalonia.Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);

        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainVM>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 1. Infrastructure Services
        services.AddSingleton<IYamlParser, ClearScriptYamlParser>();
        services.AddSingleton<IYamlSchemaValidator, YamlSchemaValidator>();
        services.AddSingleton<ILayerDomainParser, LayerDomainParser>();
        services.AddSingleton<IBoardDomainParser, BoardDomainParser>();
        services.AddSingleton<IStylesDomainParser, StylesDomainParser>();

        // 2. Application Services
        services.AddSingleton<ILogicValidationService, LogicValidationService>();
        services.AddSingleton<IDrawingGenerationService, DrawingGenerationService>();
        services.AddSingleton<IBoardProcessingCoordinator, BoardProcessingCoordinator>();

        // 3. ViewModels
        services.AddSingleton<MainVM>();
    }
}