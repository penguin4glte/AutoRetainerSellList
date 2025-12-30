using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using AutoRetainerSellList.UI;
using AutoRetainerSellList.Domain.Services;
using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Infrastructure.Persistence;
using AutoRetainerSellList.Infrastructure.GameClient;
using AutoRetainerSellList.Infrastructure.Automation;
using AutoRetainerSellList.Infrastructure.Monitoring;
using AutoRetainerSellList.Infrastructure.Localization;
using AutoRetainerSellList.Application.UseCases;
using AutoRetainerSellList.Application.Queries;

namespace AutoRetainerSellList;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Auto Retainer Sell List";
    private const string CommandName = "/arsl";

    private readonly IServiceProvider _serviceProvider;
    private readonly RetainerListMonitor _retainerListMonitor;
    private readonly MainWindow _mainWindow;
    public WindowSystem WindowSystem { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            // Initialize ECommons first (required for Svc.Log)
            ECommonsMain.Init(pluginInterface, this);

            // Build DI container
            var services = new ServiceCollection();

            // External services (Dalamud)
            services.AddSingleton(pluginInterface);

            // Domain Services
            services.AddSingleton<PricingStrategy>();

            // Infrastructure - Repositories
            services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
            services.AddSingleton<IRetainerRepository, RetainerRepository>();

            // Infrastructure - Game Services
            services.AddSingleton<GameUIService>();
            services.AddSingleton<MarketBoardService>();
            services.AddSingleton<InventoryService>();
            services.AddSingleton<TaskExecutor>();
            services.AddSingleton<AddonInteractionService>();
            services.AddSingleton<ContextMenuService>();
            services.AddSingleton<RetainerListMonitor>();

            // Infrastructure - Localization
            services.AddSingleton<ChatMessageService>();

            // Application - Use Cases
            services.AddSingleton<ExecuteSellListUseCase>();
            services.AddSingleton<ProcessAllRetainersUseCase>();
            services.AddSingleton<UpdateSellListUseCase>();

            // Application - Queries
            services.AddSingleton<GetRetainerListQuery>();
            services.AddSingleton<GetSellListQuery>();
            services.AddSingleton<SearchItemsQuery>();

            // Presentation - ViewModels
            services.AddSingleton<Presentation.UI.ViewModels.MainWindowViewModel>();
            services.AddSingleton<Presentation.UI.ViewModels.SettingsWindowViewModel>();

            // Presentation - Windows
            services.AddSingleton<MainWindow>();
            services.AddSingleton<SettingsWindow>();

            // Factory for lazy resolution
            services.AddSingleton<Func<SettingsWindow>>(sp => () => sp.GetRequiredService<SettingsWindow>());

            _serviceProvider = services.BuildServiceProvider();

            // Setup UI
            WindowSystem = new WindowSystem("AutoRetainerSellList");

            _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();

            WindowSystem.AddWindow(_mainWindow);
            WindowSystem.AddWindow(settingsWindow);

            // Setup RetainerList monitoring
            _retainerListMonitor = _serviceProvider.GetRequiredService<RetainerListMonitor>();
            _retainerListMonitor.RetainerListOpened += OnRetainerListOpened;
            _retainerListMonitor.RetainerListClosed += OnRetainerListClosed;

            // Register commands
            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Auto Retainer Sell List window"
            });

            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to initialize Auto Retainer Sell List: {ex}");
            throw;
        }
    }

    private void OnRetainerListOpened()
    {
        try
        {
            _mainWindow.IsOpen = true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[Plugin] Error in OnRetainerListOpened: {ex}");
        }
    }

    private void OnRetainerListClosed()
    {
        try
        {
            _mainWindow.IsOpen = false;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[Plugin] Error in OnRetainerListClosed: {ex}");
        }
    }

    private void OnCommand(string command, string args)
    {
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Toggle();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[Plugin] Error in OnCommand: {ex}");
        }
    }

    private void DrawUI()
    {
        try
        {
            WindowSystem.Draw();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[Plugin] Error in DrawUI(): {ex}");
        }
    }

    private void DrawConfigUI()
    {
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Toggle();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[Plugin] Error in DrawConfigUI: {ex}");
        }
    }

    public void Dispose()
    {
        try
        {
            // Unsubscribe from events
            if (_retainerListMonitor != null)
            {
                _retainerListMonitor.RetainerListOpened -= OnRetainerListOpened;
                _retainerListMonitor.RetainerListClosed -= OnRetainerListClosed;
            }

            WindowSystem.RemoveAllWindows();

            Svc.Commands.RemoveHandler(CommandName);
            Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            if (_serviceProvider is IDisposable disposable)
                disposable.Dispose();

            ECommonsMain.Dispose();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error during Auto Retainer Sell List disposal: {ex}");
        }
    }
}
