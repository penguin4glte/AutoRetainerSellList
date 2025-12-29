using Dalamud.Interface.Windowing;
using AutoRetainerSellList.Presentation.UI.ViewModels;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using static ECommons.GenericHelpers;
using Dalamud.Interface;
using ECommons.ImGuiMethods;

namespace AutoRetainerSellList.UI;

public class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Func<SettingsWindow> _getSettingsWindow;

    public MainWindow(MainWindowViewModel viewModel, Func<SettingsWindow> getSettingsWindow)
        : base("Auto Retainer Sell List##MainWindow")
    {
        _viewModel = viewModel;
        _getSettingsWindow = getSettingsWindow;

        Size = new System.Numerics.Vector2(240, 40);
        SizeCondition = ImGuiCond.Always;

        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar;
    }

    public override unsafe void PreDraw()
    {
        // Position window relative to RetainerList
        try
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerListAddon) && retainerListAddon->IsVisible)
            {
                var retainerListPos = new System.Numerics.Vector2(retainerListAddon->X, retainerListAddon->Y);
                var retainerListSize = new System.Numerics.Vector2(retainerListAddon->GetScaledWidth(true), retainerListAddon->GetScaledHeight(true));

                // Position: MainWindow's bottom-right corner at RetainerList's top-right corner
                var mainWindowSize = new System.Numerics.Vector2(240, 40);
                var newPos = new System.Numerics.Vector2(
                    retainerListPos.X + retainerListSize.X - mainWindowSize.X, // Align right edges
                    retainerListPos.Y - mainWindowSize.Y  // MainWindow bottom = RetainerList top
                );

                ImGui.SetNextWindowPos(newPos, ImGuiCond.Always);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MainWindow] Error in PreDraw(): {ex}");
        }
    }

    public override void Draw()
    {
        try
        {
            // Left side: Checkbox with label
            bool isAutoExecute = _viewModel.IsAutoExecuteEnabled;
            if (ImGui.Checkbox("Enable Auto Sell List", ref isAutoExecute))
            {
                _viewModel.ToggleAutoExecute();
            }

            // Right side: Settings button
            ImGui.SameLine();
            var buttonSize = ImGui.GetFrameHeight(); // Make icon button square
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - buttonSize - ImGui.GetStyle().WindowPadding.X);
            if (ImGuiEx.IconButton(FontAwesomeIcon.Cog, "##Settings"))
            {
                _getSettingsWindow().Toggle();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Settings");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MainWindow] Error in Draw(): {ex}");
        }
    }

    public override void OnOpen()
    {
        // Fire and forget - async initialization
        _ = _viewModel.RefreshAutoExecuteStatus();
    }
}
