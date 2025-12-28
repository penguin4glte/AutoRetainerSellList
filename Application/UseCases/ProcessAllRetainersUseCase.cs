using AutoRetainerSellList.Domain.Repositories;
using AutoRetainerSellList.Infrastructure.Automation;
using AutoRetainerSellList.Infrastructure.GameClient;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace AutoRetainerSellList.Application.UseCases;

public class ProcessAllRetainersUseCase
{
    private readonly IConfigurationRepository _configRepository;
    private readonly IRetainerRepository _retainerRepository;
    private readonly ExecuteSellListUseCase _executeSellListUseCase;
    private readonly GameUIService _gameUIService;
    private readonly TaskExecutor _taskExecutor;

    private List<ulong> _retainersToProcess = new();
    private int _currentRetainerIndex = 0;

    public bool IsRunning => _taskExecutor.IsBusy;

    public ProcessAllRetainersUseCase(
        IConfigurationRepository configRepository,
        IRetainerRepository retainerRepository,
        ExecuteSellListUseCase executeSellListUseCase,
        GameUIService gameUIService,
        TaskExecutor taskExecutor)
    {
        _configRepository = configRepository;
        _retainerRepository = retainerRepository;
        _executeSellListUseCase = executeSellListUseCase;
        _gameUIService = gameUIService;
        _taskExecutor = taskExecutor;
    }

    public async Task<bool> StartProcessingAsync()
    {
        try
        {

            _retainersToProcess.Clear();
            _currentRetainerIndex = 0;

            // Get all sell lists first (await outside unsafe)
            var sellLists = await _configRepository.LoadAllSellListsAsync();

            // Get all retainers with sell lists (unsafe block for pointer access only)
            bool retainerManagerNull = false;
            unsafe
            {
                var retainerManager = RetainerManager.Instance();
                if (retainerManager == null)
                {
                    retainerManagerNull = true;
                }
                else
                {
                    var retainerSpan = retainerManager->Retainers;

                    for (var i = 0; i < retainerSpan.Length; i++)
                    {
                        var retainer = retainerSpan[i];
                        var retainerId = retainer.RetainerId;

                        if (!retainer.Available || retainerId == 0)
                            continue;

                        var retainerIdVo = new Domain.ValueObjects.RetainerId(retainerId);
                        if (sellLists.ContainsKey(retainerIdVo))
                        {
                            _retainersToProcess.Add(retainerId);
                        }
                    }
                }
            }

            if (retainerManagerNull)
            {
                Svc.Log.Error("[ProcessAllRetainersUseCase] RetainerManager is null");
                await FinishProcessing();
                return false;
            }

            if (_retainersToProcess.Count == 0)
            {
                Svc.Log.Warning("[ProcessAllRetainersUseCase] No retainers with sell lists found");
                await FinishProcessing();
                return false;
            }


            // Start processing
            ProcessNextRetainer();
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ProcessAllRetainersUseCase] Error: {ex}");
            return false;
        }
    }

    private void ProcessNextRetainer()
    {
        if (_currentRetainerIndex >= _retainersToProcess.Count)
        {
            _taskExecutor.Enqueue(() =>
            {
                FinishProcessing().GetAwaiter().GetResult();
                return true;
            });
            return;
        }

        var retainerId = _retainersToProcess[_currentRetainerIndex];

        // Step 1: Select retainer from RetainerList
        _taskExecutor.Enqueue(() => _gameUIService.SelectRetainerFromList(_currentRetainerIndex), $"SelectRetainer_{_currentRetainerIndex}");
        _taskExecutor.EnqueueDelay(500);

        // Step 2: Click "Check Selling Items" in SelectString menu
        _taskExecutor.Enqueue(() => _gameUIService.OpenCheckSellingItems(), $"OpenSellingItems_{_currentRetainerIndex}");
        _taskExecutor.EnqueueDelay(500);

        // Step 3: Wait for RetainerSellList addon to open
        _taskExecutor.Enqueue(() => _gameUIService.WaitForRetainerSellListAddon(), $"WaitSellingList_{_currentRetainerIndex}");
        _taskExecutor.EnqueueDelay(200);

        // Step 4: Execute sell list (enqueues tasks immediately, then enqueue cleanup)
        _taskExecutor.Enqueue(() =>
        {
            _executeSellListUseCase.ExecuteAsync(retainerId, () => OnRetainerCompleted()).GetAwaiter().GetResult();
            return true;
        }, $"ExecuteSellList_{_currentRetainerIndex}");
    }

    private void OnRetainerCompleted()
    {

        // Close RetainerSellList addon
        _taskExecutor.Enqueue(() => _gameUIService.CloseRetainerSellListAddon(), $"CloseSellingList_{_currentRetainerIndex}");
        _taskExecutor.EnqueueDelay(300);

        // Close retainer menu (SelectString)
        _taskExecutor.Enqueue(() => _gameUIService.CloseRetainerMenu(), $"CloseRetainerMenu_{_currentRetainerIndex}");
        _taskExecutor.EnqueueDelay(500);

        // Move to next retainer
        _taskExecutor.Enqueue(() =>
        {
            _currentRetainerIndex++;
            ProcessNextRetainer();
            return true;
        }, $"NextRetainer_{_currentRetainerIndex}");
    }

    private async Task FinishProcessing()
    {
        await _configRepository.SetAutoExecuteEnabledAsync(false);
        await _configRepository.SaveAsync();
    }

    public void Abort()
    {
        _taskExecutor.Abort();
        _retainersToProcess.Clear();
        _currentRetainerIndex = 0;
    }
}
