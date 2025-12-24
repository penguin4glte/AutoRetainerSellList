using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace RetainerPriceAdjuster.UI;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base(
        "Retainer Price Adjuster",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var config = plugin.Configuration;

        // Status display
        ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), "Status:");
        ImGui.SameLine();

        var taskManager = plugin.TaskManager;
        if (taskManager.IsRunning)
        {
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Running...");
            ImGui.Text($"Current Task: {taskManager.CurrentTaskDescription}");
            ImGui.Text($"Progress: {taskManager.CurrentRetainerIndex + 1}/{taskManager.TotalRetainers}");
        }
        else if (taskManager.LastError != null)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Error");
            ImGui.TextWrapped($"Error: {taskManager.LastError}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Idle");
        }

        ImGui.Separator();

        // Main control checkbox
        bool isEnabled = config.IsPriceAdjustmentEnabled;
        if (ImGui.Checkbox("価格調整を開始する", ref isEnabled))
        {
            config.IsPriceAdjustmentEnabled = isEnabled;
            config.Save();

            if (isEnabled)
            {
                // Start price adjustment
                Plugin.Log.Info("Starting price adjustment");
                taskManager.StartPriceAdjustment();
            }
            else
            {
                // Stop price adjustment
                Plugin.Log.Info("Stopping price adjustment");
                taskManager.Stop();
            }
        }

        ImGui.Separator();

        // Settings section
        if (ImGui.CollapsingHeader("Settings"))
        {
            bool autoOpen = config.AutoOpenNearBell;
            if (ImGui.Checkbox("Auto-open window near bell", ref autoOpen))
            {
                config.AutoOpenNearBell = autoOpen;
                config.Save();
            }

            float proximity = config.BellProximityDistance;
            if (ImGui.SliderFloat("Bell proximity distance", ref proximity, 1.0f, 10.0f, "%.1f"))
            {
                config.BellProximityDistance = proximity;
                config.Save();
            }

            int delayRetainers = config.DelayBetweenRetainers;
            if (ImGui.SliderInt("Delay between retainers (ms)", ref delayRetainers, 500, 3000))
            {
                config.DelayBetweenRetainers = delayRetainers;
                config.Save();
            }

            int delayUpdates = config.DelayBetweenPriceUpdates;
            if (ImGui.SliderInt("Delay between price updates (ms)", ref delayUpdates, 200, 2000))
            {
                config.DelayBetweenPriceUpdates = delayUpdates;
                config.Save();
            }

            bool debugLogging = config.EnableDebugLogging;
            if (ImGui.Checkbox("Enable debug logging", ref debugLogging))
            {
                config.EnableDebugLogging = debugLogging;
                config.Save();
            }
        }

        // Debug info
        if (config.EnableDebugLogging && ImGui.CollapsingHeader("Debug Info"))
        {
            ImGui.Text($"Is Near Bell: {plugin.BellProximityService.IsNearBell}");
            ImGui.Text($"Bell Distance: {plugin.BellProximityService.DistanceToBell:F2}");
            ImGui.Text($"Task Queue Size: {taskManager.TaskQueueSize}");
        }
    }
}
