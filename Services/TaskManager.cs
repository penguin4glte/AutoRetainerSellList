using System;
using System.Collections.Generic;
using System.Linq;

namespace RetainerPriceAdjuster.Services;

public class TaskManager : IDisposable
{
    private readonly Plugin plugin;
    private readonly Queue<TaskEntry> taskQueue = new();
    private TaskEntry? currentTask = null;
    private long lastTaskExecutionTime = 0;
    private const int MinTaskDelay = 100; // Minimum delay between tasks in ms

    public bool IsRunning => currentTask != null || taskQueue.Count > 0;
    public string? CurrentTaskDescription => currentTask?.Description;
    public int TaskQueueSize => taskQueue.Count;
    public string? LastError { get; private set; } = null;

    // Progress tracking
    public int CurrentRetainerIndex { get; private set; } = 0;
    public int TotalRetainers { get; private set; } = 0;

    private class TaskEntry
    {
        public Func<bool?> Action { get; set; } = null!;
        public string Description { get; set; } = "";
        public int MaxAttempts { get; set; } = 100;
        public int CurrentAttempt { get; set; } = 0;
        public long DelayMs { get; set; } = 0;
        public long EnqueuedAt { get; set; } = 0;
    }

    public TaskManager(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Update()
    {
        if (!IsRunning)
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Check if enough time has passed since last execution
        if (now - lastTaskExecutionTime < MinTaskDelay)
            return;

        // Execute current task or get next from queue
        if (currentTask == null && taskQueue.Count > 0)
        {
            currentTask = taskQueue.Dequeue();
            if (plugin.Configuration.EnableDebugLogging)
            {
                Plugin.Log.Info($"Starting task: {currentTask.Description}");
            }
        }

        if (currentTask == null)
            return;

        // Check if we need to wait for delay
        if (currentTask.DelayMs > 0)
        {
            if (now - currentTask.EnqueuedAt < currentTask.DelayMs)
                return;
        }

        try
        {
            var result = currentTask.Action();

            if (result == true)
            {
                // Task completed successfully
                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"Task completed: {currentTask.Description}");
                }
                currentTask = null;
                lastTaskExecutionTime = now;
            }
            else if (result == false)
            {
                // Task still running, increment attempt
                currentTask.CurrentAttempt++;
                if (currentTask.CurrentAttempt >= currentTask.MaxAttempts)
                {
                    Plugin.Log.Warning($"Task timed out: {currentTask.Description}");
                    LastError = $"Task timed out: {currentTask.Description}";
                    Stop();
                }
                lastTaskExecutionTime = now;
            }
            // If result is null, task is waiting for something, don't increment attempt
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Task error: {currentTask?.Description ?? "Unknown"}");
            LastError = $"{currentTask?.Description ?? "Unknown"}: {ex.Message}";
            Stop();
        }
    }

    public void Enqueue(Func<bool?> action, string description, int maxAttempts = 100, long delayMs = 0)
    {
        taskQueue.Enqueue(new TaskEntry
        {
            Action = action,
            Description = description,
            MaxAttempts = maxAttempts,
            DelayMs = delayMs,
            EnqueuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    public void EnqueueDelay(long delayMs, string description = "Waiting")
    {
        Enqueue(() => true, description, maxAttempts: 1, delayMs: delayMs);
    }

    public void Stop()
    {
        taskQueue.Clear();
        currentTask = null;
        CurrentRetainerIndex = 0;
        TotalRetainers = 0;

        // Turn off the checkbox
        if (plugin.Configuration.IsPriceAdjustmentEnabled)
        {
            plugin.Configuration.IsPriceAdjustmentEnabled = false;
            plugin.Configuration.Save();
        }

        Plugin.Log.Info("Task manager stopped");
    }

    public void StartPriceAdjustment()
    {
        if (IsRunning)
        {
            Plugin.Log.Warning("Task manager is already running");
            return;
        }

        LastError = null;
        CurrentRetainerIndex = 0;

        // Build task queue for price adjustment workflow
        BuildPriceAdjustmentTasks();
    }

    private void BuildPriceAdjustmentTasks()
    {
        // Step 1: Open retainer list
        Enqueue(
            () => plugin.RetainerService.OpenRetainerList(),
            "Opening retainer list"
        );

        // Step 2: Get retainer count
        Enqueue(
            () => plugin.RetainerService.GetRetainerCount(),
            "Getting retainer count"
        );

        // Step 3: Set total retainers
        Enqueue(() =>
        {
            TotalRetainers = plugin.RetainerService.RetainerCount;
            if (TotalRetainers == 0)
            {
                Plugin.Log.Warning("No retainers found");
                Stop();
                return false;
            }
            Plugin.Log.Info($"Found {TotalRetainers} retainers");
            return true;
        }, "Setting up retainer processing");

        // Step 4: Process each retainer
        for (int i = 0; i < 10; i++) // Max 10 retainers
        {
            int retainerIndex = i;

            // Update progress
            Enqueue(() =>
            {
                CurrentRetainerIndex = retainerIndex;
                return CurrentRetainerIndex < TotalRetainers;
            }, $"Checking retainer {retainerIndex + 1}");

            // If we've processed all retainers, stop
            Enqueue(() =>
            {
                if (CurrentRetainerIndex >= TotalRetainers)
                {
                    Stop();
                    return null; // Skip remaining tasks
                }
                return true;
            }, "Checking if done");

            // Select retainer
            Enqueue(
                () => plugin.RetainerService.SelectRetainer(retainerIndex),
                $"Selecting retainer {retainerIndex + 1}"
            );

            EnqueueDelay(500, "Waiting for dialogue");

            // Wait for retainer menu to open (user needs to click Talk manually)
            Enqueue(
                () => plugin.RetainerService.WaitForRetainerMenu(),
                "Waiting for retainer menu to open",
                maxAttempts: 300  // 30 seconds timeout (300 * 100ms)
            );

            EnqueueDelay(300, "Retainer menu opened");

            // Open sell list
            Enqueue(
                () => plugin.RetainerService.OpenSellList(),
                "Opening sell list"
            );

            EnqueueDelay(500, "Waiting for sell list");

            // Get listings
            Enqueue(
                () => plugin.RetainerService.GetCurrentListings(),
                "Getting current listings"
            );

            // Process each listing
            Enqueue(
                () => ProcessListings(),
                "Processing listings"
            );

            // Close retainer
            Enqueue(
                () => plugin.RetainerService.CloseRetainer(),
                "Closing retainer"
            );

            EnqueueDelay(plugin.Configuration.DelayBetweenRetainers, "Waiting before next retainer");
        }

        // Final step: Close retainer list
        Enqueue(
            () => plugin.RetainerService.CloseRetainerList(),
            "Closing retainer list"
        );

        // Stop when done
        Enqueue(() =>
        {
            Plugin.Log.Info("Price adjustment completed");
            Stop();
            return true;
        }, "Finishing up");
    }

    private bool? ProcessListings()
    {
        var listings = plugin.RetainerService.CurrentListings;
        if (listings.Count == 0)
        {
            Plugin.Log.Info("No listings to process");
            return true;
        }

        Plugin.Log.Info($"Processing {listings.Count} listings");

        // Process each listing sequentially
        foreach (var listing in listings)
        {
            var currentListing = listing; // Capture for closure

            // Step 1: Click on the listing item
            Enqueue(
                () => plugin.RetainerService.ClickListingItem(currentListing.SlotIndex),
                $"Clicking {currentListing.ItemName} ({(currentListing.IsHq ? "HQ" : "NQ")})"
            );

            EnqueueDelay(300, "Waiting for window to open");

            // Step 2: Wait for ItemSearchResult (MarketBuddy) or InputNumeric
            Enqueue(
                () => plugin.RetainerService.WaitForItemSearchResult(),
                "Waiting for market data window"
            );

            EnqueueDelay(200, "Waiting for data to load");

            // Step 3: Read price from ItemSearchResult if it's open
            Enqueue(
                () => plugin.MarketBoardService.ReadPriceFromItemSearchResult(currentListing.ItemId, currentListing.IsHq),
                $"Reading market price for {currentListing.ItemName}"
            );

            EnqueueDelay(100, "Processing price data");

            // Step 4: Close ItemSearchResult window
            Enqueue(
                () => plugin.RetainerService.CloseItemSearchResult(),
                "Closing market data window"
            );

            EnqueueDelay(200, "Waiting for InputNumeric");

            // Step 5: Wait for InputNumeric dialog to be ready
            Enqueue(
                () => plugin.RetainerService.WaitForInputNumeric(),
                "Waiting for price input dialog"
            );

            EnqueueDelay(100, "Dialog ready");

            // Step 6: Calculate and set the new price (lowest price - 1 gil)
            Enqueue(() =>
            {
                var marketPrice = plugin.MarketBoardService.GetLowestPrice(currentListing.ItemId, currentListing.IsHq);
                if (marketPrice == null)
                {
                    Plugin.Log.Warning($"Could not get market price for {currentListing.ItemName}, skipping");
                    // Cancel the InputNumeric dialog
                    plugin.RetainerService.CancelInputNumericDialog();
                    return true;
                }

                // Calculate new price: lowest - 1 gil (minimum 1 gil)
                var newPrice = marketPrice.Value > 1 ? marketPrice.Value - 1 : 1;

                if (plugin.Configuration.EnableDebugLogging)
                {
                    Plugin.Log.Info($"{currentListing.ItemName}: Current={currentListing.CurrentPrice}, Market={marketPrice.Value}, New={newPrice}");
                }

                // Set the price
                return plugin.RetainerService.SetPriceInDialog(newPrice);
            }, $"Setting price for {currentListing.ItemName}");

            EnqueueDelay(200, "Confirming price");

            // Step 7: Confirm the price change
            Enqueue(
                () => plugin.RetainerService.ConfirmPriceDialog(),
                "Confirming price change"
            );

            EnqueueDelay(plugin.Configuration.DelayBetweenPriceUpdates, "Waiting after price update");
        }

        return true;
    }

    public void Dispose()
    {
        Stop();
    }
}
