# Auto Retainer Sell List

A Dalamud plugin for FFXIV that automatically manages retainer sell listings with smart pricing based on market board data.

## Features

- **Automated Sell List Management**: Configure a list of items to maintain on sale across your retainers
- **Smart Pricing**: Automatically sets prices at (lowest market price - 1 gil) to undercut competition
- **Automatic Window Detection**: Opens the management window automatically when you access your retainer list
- **Multi-Retainer Support**: Process all your retainers automatically
- **DDD Architecture**: Built with Domain-Driven Design principles for maintainability and testability

## Usage

1. Install the plugin through Dalamud Plugin Installer
2. Use `/arsl` command to open the settings window
3. Configure your sell list with items you want to maintain on sale
4. Access your retainer list - the plugin window will open automatically
5. The plugin will monitor market prices and adjust your retainer listings accordingly

## Architecture

This plugin follows a clean architecture with clear separation of concerns:

- **Domain Layer**: Core business logic and domain models
- **Application Layer**: Use cases and queries
- **Infrastructure Layer**: Game client integration, persistence, and automation
- **Presentation Layer**: UI and ViewModels

## Building

### Prerequisites

- .NET 10.0 SDK
- Dalamud development environment
- Visual Studio 2022 or Rider (recommended)

### Setup

1. Clone the repository:
```bash
git clone <repository-url>
cd AutoRetainerSellList
```

2. Initialize submodules:
```bash
git submodule update --init --recursive
```

3. Build the project:
```bash
dotnet build
```

The compiled plugin will be output to `bin/Debug/` or `bin/Release/` depending on your build configuration.

### Setting up on a Different Machine

When cloning this repository on a new machine, always initialize the submodules:

```bash
git clone <repository-url>
cd AutoRetainerSellList
git submodule update --init --recursive
```

This ensures the ECommons library is properly downloaded.

## Dependencies

- [ECommons](https://github.com/NightmareXIV/ECommons) - Included as a Git submodule
- Dalamud Plugin Framework
- FFXIVClientStructs
- Microsoft.Extensions.DependencyInjection

## Development

The project includes comprehensive unit tests using xUnit, Moq, and FluentAssertions. To run tests:

```bash
dotnet test
```

## License

This project is licensed under the terms specified in the repository.

## Contributing

Contributions are welcome! Please ensure all tests pass and follow the existing code structure when submitting pull requests.
