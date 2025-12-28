using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using static ECommons.GenericHelpers;

namespace AutoRetainerSellList.Infrastructure.Monitoring;

public class RetainerListMonitor : IDisposable
{
    private bool _wasOpen = false;

    public event Action? RetainerListOpened;
    public event Action? RetainerListClosed;

    public bool IsRetainerListOpen { get; private set; }

    public RetainerListMonitor()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            var retainerListAddon = Svc.GameGui.GetAddonByName("RetainerList");
            var isCurrentlyOpen = retainerListAddon != IntPtr.Zero;

            if (isCurrentlyOpen != _wasOpen)
            {
                _wasOpen = isCurrentlyOpen;
                IsRetainerListOpen = isCurrentlyOpen;

                if (isCurrentlyOpen)
                {
                    RetainerListOpened?.Invoke();
                }
                else
                {
                    RetainerListClosed?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[RetainerListMonitor] Error in framework update: {ex}");
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
    }
}
