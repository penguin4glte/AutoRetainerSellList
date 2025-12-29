# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A FFXIV Dalamud plugin that automatically manages retainer sell listings with smart market-based pricing. Built with Domain-Driven Design (DDD) architecture and comprehensive test coverage.

## Build Commands

### Basic Operations
```bash
# Build the project
dotnet build

# Build for release
dotnet build -c Release

# Run all tests
dotnet test

# Run tests for specific project
dotnet test Tests/Domain.Tests/Domain.Tests.csproj
dotnet test Tests/Application.Tests/Application.Tests.csproj
```

### Setup Requirements
- Must initialize git submodules after cloning: `git submodule update --init --recursive`
- ECommons library is included as a git submodule at `ECommons/`
- Requires .NET 10.0 SDK and Dalamud development environment
- DalamudLibPath defaults to `$(appdata)\XIVLauncher\addon\Hooks\dev\`

## Architecture

The codebase follows strict Domain-Driven Design with clear layer separation:

### Domain Layer (`Domain/`)
Pure business logic with no external dependencies. Contains:
- **Aggregates/**: `SellListAggregate` - manages the collection of items to sell (max 20 items per retainer)
- **Entities/**: `Retainer`, `SellListItem` - core domain entities
- **ValueObjects/**: `ItemId`, `Price`, `Quantity`, `RetainerId` - immutable value types
- **Services/**: `PricingStrategy` - pricing calculation (lowest market price - 1 gil, minimum 1 gil)
- **Repositories/**: Interfaces only (`IConfigurationRepository`, `IRetainerRepository`)

### Application Layer (`Application/`)
Orchestrates domain logic and coordinates use cases:
- **UseCases/**:
  - `ExecuteSellListUseCase` - processes a single retainer's sell list
  - `ProcessAllRetainersUseCase` - automates processing across all retainers with configured sell lists
  - `UpdateSellListUseCase` - manages sell list configuration changes
- **Queries/**: `GetRetainerListQuery`, `GetSellListQuery`, `SearchItemsQuery`
- **DTOs/**: Data transfer objects for cross-layer communication

### Infrastructure Layer (`Infrastructure/`)
Implements technical concerns and external integrations:
- **GameClient/**:
  - `GameUIService` - UI addon interaction (RetainerList, RetainerSellList, SelectString)
  - `MarketBoardService` - market price queries via Universalis API
  - `InventoryService` - inventory and retainer inventory access
  - `RetainerRepository` - retainer data access implementation
- **Automation/**:
  - `TaskExecutor` - wraps ECommons TaskManager for sequential task execution (default 5min timeout)
  - `AddonInteractionService` - low-level addon manipulation
  - `ContextMenuService` - context menu integration
- **Monitoring/**: `RetainerListMonitor` - detects RetainerList addon open/close events
- **Persistence/**: `ConfigurationRepository` - JSON-based config storage

### Presentation Layer (`Presentation/` and `UI/`)
User interface components:
- **UI/**: `MainWindow` (auto-opens on RetainerList), `SettingsWindow` (manual via `/arsl`)
- **ViewModels/**: `MainWindowViewModel`, `SettingsWindowViewModel`

### Dependency Injection
The `Plugin.cs` class configures all DI using `Microsoft.Extensions.DependencyInjection`. Services are registered as singletons and resolved through the service provider.

## Key Domain Concepts

### SellListAggregate
- Maintains up to 20 items per retainer (hard limit)
- Enforces uniqueness by ItemId
- Each item has a GUID for UI tracking
- Immutable SellListItem entities (replace entire item to update)

### Automation Flow
The `ProcessAllRetainersUseCase` demonstrates the automation pattern:
1. Uses `TaskExecutor` to enqueue sequential async tasks
2. Each task returns `bool?` (true=done, false=retry, null=fail)
3. Tasks include delays between operations (200-500ms typical)
4. Flow: Select retainer → Open selling items menu → Wait for addon → Execute sell list → Close menus → Next retainer

### Pricing Strategy
Business rule: Always undercut market by 1 gil, but never price below 1 gil. This is the core value proposition.

## Testing

Tests use xUnit, Moq, and FluentAssertions:
- `Domain.Tests/` - pure domain logic tests, no mocking needed
- `Application.Tests/` - use case testing with mocked repositories and services

Test structure mirrors source code structure for easy navigation.

## Important Notes

- All unsafe code (FFXIVClientStructs pointers) is isolated in GameClient services
- ECommons provides automation utilities (TaskManager, Svc for Dalamud services)
- Configuration is per-retainer and persisted as JSON
- The plugin auto-opens MainWindow when RetainerList addon appears (via RetainerListMonitor)
- Command `/arsl` opens settings manually
