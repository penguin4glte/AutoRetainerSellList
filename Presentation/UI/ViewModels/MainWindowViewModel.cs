using AutoRetainerSellList.Application.UseCases;
using AutoRetainerSellList.Domain.Repositories;
using ECommons.DalamudServices;

namespace AutoRetainerSellList.Presentation.UI.ViewModels;

public class MainWindowViewModel
{
    private readonly ProcessAllRetainersUseCase _processAllUseCase;
    private readonly IConfigurationRepository _configRepository;

    public bool IsAutoExecuteEnabled { get; private set; }
    public bool IsProcessing => _processAllUseCase.IsRunning;

    public MainWindowViewModel(
        ProcessAllRetainersUseCase processAllUseCase,
        IConfigurationRepository configRepository)
    {
        _processAllUseCase = processAllUseCase;
        _configRepository = configRepository;

        // Don't call async method in constructor - will be called in OnOpen
    }

    public async void ToggleAutoExecute()
    {
        try
        {
            IsAutoExecuteEnabled = !IsAutoExecuteEnabled;
            await _configRepository.SetAutoExecuteEnabledAsync(IsAutoExecuteEnabled);
            await _configRepository.SaveAsync();

            if (IsAutoExecuteEnabled)
            {
                await _processAllUseCase.StartProcessingAsync();
            }
            else
            {
                _processAllUseCase.Abort();
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MainWindowViewModel] Error toggling auto-execute: {ex}");
        }
    }

    public async Task RefreshAutoExecuteStatus()
    {
        try
        {
            IsAutoExecuteEnabled = await _configRepository.GetAutoExecuteEnabledAsync();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[MainWindowViewModel] Error refreshing auto-execute status: {ex}");
        }
    }
}
