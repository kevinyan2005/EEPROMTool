# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WPF desktop application (.NET Framework 4.7.2) for reading, writing, and managing data on Dallas Semiconductor DS2431 1-Wire EEPROM chips via a DS9490 USB adapter. The tool manages three distinct data blocks in 128 bytes of EEPROM memory.

## Build Commands

```powershell
# Build the solution
msbuild OneWireEEPROMWpfApp.sln

# Build specific configuration
msbuild OneWireEEPROMWpfApp.sln /p:Configuration=Release

# Run tests (NUnit 3.x)
dotnet test OneWire.Tests\OneWire.Tests.csproj

# Clean all bin/ and obj/ folders
.\Clean-BuildFolders.bat
```

The `.proj` file (`OneWireEEPROMWpfApp.proj`) drives the full Monteris build pipeline (Build, Test, Document, Package targets) using custom MSBuild targets in `Build/Targets/`.

## Architecture

### Projects

| Project | Type | Role |
|---|---|---|
| `OneWireEEPROMWpfApp` | WPF App | UI shell, ViewModels, Views |
| `OneWireController` | Library | DS2431 hardware communication |
| `OneWire.Common` | Library | Data models, byte utilities, CRC |
| `OneWire.Tests` | NUnit | Tests for Common utilities |

### EEPROM Data Layout (128 bytes)

| Bytes | Block | Class |
|---|---|---|
| 0–35 | Identification | `OneWireIdentificationBlock` |
| 38–75 | Sensor Calibration | `SensorCalibrationBlock` |
| 80–127 | User Defined | `UserDefinedBlock` |

Each block implements `IBlockWithCrc` and has a `ToBytes()` method that returns the exact byte layout with CRC appended. CRCs are Dallas/Maxim CRC16 (see `CrcHelper`).

### MVVM Structure

`MainViewModel` is the root ViewModel. It owns:
- `IdentificationViewModel` → wraps `OneWireIdentificationBlock`
- `CalibrationViewModel` → wraps `SensorCalibrationBlock`  
- `UserDataViewModel` → wraps `UserDefinedBlock`

All ViewModels inherit `ViewModelBase` (INotifyPropertyChanged). Commands use `RelayCommand`. `FileDialogService` is injected into `MainViewModel` for file open/save dialogs.

### Hardware Layer

`DS2431Helper` in `OneWireController` wraps the `OneWireLinkLayer` NuGet library:
- Reads 128 bytes in 4 pages of 32 bytes each (page-aligned reads)
- Writes via Scratchpad protocol: Write Scratchpad → Read Scratchpad → Copy Scratchpad
- Supports standard (`SPEED_REGULAR`) and overdrive (`SPEED_OVERDRIVE`) modes
- Key ROM commands: `0xCC` (Skip ROM), `0x3C` (Overdrive), `0xF0` (Read Memory), `0x0F` (Write Scratchpad)
- All operations are async with `IProgress<int>` reporting

### Byte Encoding Conventions

Different fields use different endianness — this is a vendor format constraint:
- **Big-endian**: Identification version, CRC values
- **Word-swapped** (custom vendor): Gauge factors, reference values — use `ByteHelper.ReadWordSwappedFloat` / `WriteWordSwappedFloat`
- **Little-endian**: Schema, probe serial (ASCII)
- **DateTime**: 8-byte vendor format — use `ByteHelper.ToDateTime` / `FromDateTime`

### File I/O

- **JSON**: Newtonsoft.Json, pretty-printed, ISO 8601 dates
- **Raw hex**: Space-separated hex bytes parsed with regex `\b(?:0x)?([0-9A-Fa-f]{2})\b`

### Key Validation Pattern

`UserDataViewModel` parses probe serial numbers in the format `partNumber-size-lot-sequence` and validates via `IDataErrorInfo`. All ViewModels recalculate their block's CRC whenever a property changes — the CRC displayed in the UI is always live.

## Dependencies

- `OneWireLinkLayer` 4.1.x — Dallas Semiconductor 1-Wire adapter SDK (not available on NuGet.org; in `packages/`)
- `Newtonsoft.Json` 13.0.3 — JSON serialization
- `NLog` 4.6.8 + `NlogViewer` 0.7.0 — logging with in-app viewer panel
- `slf4net` + `slf4net.NLog` — logging abstraction used throughout `OneWireController`
- `mmi.utils` 9.2.0 — Monteris internal utilities

## Testing

Tests are in `OneWire.Tests/` covering `ByteHelper` (endianness, word-swapping, datetime encoding) and `CrcHelper` (CRC16/CRC8 algorithms). Test these when modifying anything in `OneWire.Common/Helpers/`.
