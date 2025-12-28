using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;

namespace AutoRetainerSellList.Services;

public unsafe class RetainerItemCommandService : IDisposable
{
    public enum RetainerItemCommand : long
    {
        RetrieveFromRetainer = 0,
        EntrustToRetainer = 1,
        EntrustQuantity = 4,
        HaveRetainerSellItem = 5,
    }

    internal delegate void RetainerItemCommandDelegate(nint agentRetainerItemCommandModule, uint slot, InventoryType inventoryType, uint a4, RetainerItemCommand command);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 5C 24 ?? 41 8B F0", DetourName = nameof(RetainerItemCommandDetour))]
    internal Hook<RetainerItemCommandDelegate>? RetainerItemCommandHook;

    public static nint AgentRetainerItemCommandModule => (nint)AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer) + 40;

    public RetainerItemCommandService()
    {
        try
        {
            Svc.Log.Info("Initializing RetainerItemCommandService...");
            Svc.Hook.InitializeFromAttributes(this);

            if (RetainerItemCommandHook == null)
            {
                Svc.Log.Error("CRITICAL: RetainerItemCommandHook is still NULL after InitializeFromAttributes!");
                Svc.Log.Error("The signature '48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 5C 24 ?? 41 8B F0' was not found in memory");
            }
            else
            {
                Svc.Log.Info("RetainerItemCommandHook initialized successfully");
            }

            Svc.Log.Info("RetainerItemCommandService initialization complete");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to initialize RetainerItemCommandService: {ex}");
        }
    }

    internal void RetainerItemCommandDetour(nint agentRetainerItemCommandModule, uint slot, InventoryType inventoryType, uint a4, RetainerItemCommand command)
    {
        try
        {
            Svc.Log.Debug($"RetainerItemCommand: slot={slot}, type={inventoryType}, command={command}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Error in RetainerItemCommandDetour: {e}");
        }
        RetainerItemCommandHook?.Original(agentRetainerItemCommandModule, slot, inventoryType, a4, command);
    }

    public void ExecuteCommand(uint slot, InventoryType inventoryType, RetainerItemCommand command)
    {
        Svc.Log.Info($"ExecuteCommand called: slot={slot}, type={inventoryType}, command={command}");

        if (RetainerItemCommandHook == null)
        {
            Svc.Log.Error("CRITICAL: RetainerItemCommandHook is NULL - hook was not initialized!");
            Svc.Log.Error("This means the memory signature was not found or Svc.Hook.InitializeFromAttributes failed");
            return;
        }

        Svc.Log.Info($"Hook is initialized, calling original function at AgentModule: {AgentRetainerItemCommandModule:X16}");

        try
        {
            RetainerItemCommandHook.Original(AgentRetainerItemCommandModule, slot, inventoryType, 0, command);
            Svc.Log.Info("RetainerItemCommand executed successfully");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to execute RetainerItemCommand: {ex}");
        }
    }

    public void Dispose()
    {
        RetainerItemCommandHook?.Dispose();
    }
}
