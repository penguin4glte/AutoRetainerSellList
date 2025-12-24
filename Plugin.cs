using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using RetainerPriceAdjuster.UI;
using RetainerPriceAdjuster.Services;
using System;

namespace RetainerPriceAdjuster;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Retainer Price Adjuster";
    private const string CommandName = "/rpa";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem { get; init; }
    private MainWindow MainWindow { get; init; }

    // Services
    public BellProximityService BellProximityService { get; init; }
    public RetainerService RetainerService { get; init; }
    public MarketBoardService MarketBoardService { get; init; }
    public TaskManager TaskManager { get; init; }

    public Plugin()
    {
        // Initialize ECommons
        ECommons.ECommons.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize services
        BellProximityService = new BellProximityService(this);
        RetainerService = new RetainerService(this);
        MarketBoardService = new MarketBoardService(this);
        TaskManager = new TaskManager(this);

        // Initialize UI
        WindowSystem = new WindowSystem("RetainerPriceAdjuster");
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        // Register commands
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Retainer Price Adjuster window"
        });

        // Register event handlers
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Framework.Update += OnFrameworkUpdate;

        Log.Info("Retainer Price Adjuster initialized");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

        WindowSystem.RemoveAllWindows();
        MainWindow?.Dispose();

        CommandManager.RemoveHandler(CommandName);

        BellProximityService?.Dispose();
        RetainerService?.Dispose();
        MarketBoardService?.Dispose();
        TaskManager?.Dispose();

        // Dispose ECommons
        ECommons.ECommons.Dispose();

        Log.Info("Retainer Price Adjuster disposed");
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.IsOpen = !MainWindow.IsOpen;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    private void DrawConfigUI()
    {
        MainWindow.IsOpen = true;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        BellProximityService.Update();
        TaskManager.Update();
    }
}
