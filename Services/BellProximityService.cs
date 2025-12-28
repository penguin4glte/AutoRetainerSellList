using ECommons.DalamudServices;
using System;

namespace AutoRetainerSellList.Services;

public class BellProximityService : IDisposable
{
    private bool wasRetainerListOpen = false;

    public event Action? OnRetainerListChanged;
    public bool IsRetainerListOpen { get; private set; }

    public BellProximityService()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(object framework)
    {
        var retainerListAddon = Svc.GameGui.GetAddonByName("RetainerList");
        bool currentlyRetainerListOpen = retainerListAddon != IntPtr.Zero;

        if (currentlyRetainerListOpen != wasRetainerListOpen)
        {
            IsRetainerListOpen = currentlyRetainerListOpen;
            wasRetainerListOpen = currentlyRetainerListOpen;
            OnRetainerListChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
    }
}
