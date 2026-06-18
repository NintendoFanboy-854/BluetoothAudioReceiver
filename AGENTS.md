# AGENTS.md

## Build & Run

```bash
dotnet build          # Debug build
dotnet run            # Run from source
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true   # Standalone .exe
```

Target: `net8.0-windows10.0.19041.0`, runtime `win-x64`. Requires Windows SDK — **cannot compile on Linux/macOS**.

## Architecture

WPF desktop app (MVVM) using CommunityToolkit.Mvvm source generators. Both `UseWPF` and `UseWindowsForms` are enabled in the csproj (WinForms is used for NotifyIcon).

| Directory | Purpose |
|---|---|
| `Models/` | `AppSettings` (JSON persistence), `BluetoothDevice` (observable model) |
| `ViewModels/` | `MainViewModel` — owns all UI state and commands |
| `Services/` | `BluetoothService` (DeviceWatcher), `AudioService` (AudioPlaybackConnection), `TrayIconService`, `VolumeService`, `AutoStartService`, `LocalizationService` |
| `Themes/` | `DarkTheme.xaml` — all custom control styles (AMOLED black) |
| `Converters.cs` | `SliderFillConverter` (multi-value) |
| `MainWindow.xaml.cs` | Window code-behind, plus `CanConnectConverter` (multi-value) |

## Key conventions

### CommunityToolkit.Mvvm source generators
- `[ObservableProperty]` on a field `_foo` generates property `Foo`. **Never create the property manually.**
- `[RelayCommand]` on a method `DoSomething()` generates `DoSomethingCommand`.
- `[NotifyPropertyChangedFor(nameof(OtherProperty))]` cascades change notification.

### LocalizationService
- Singleton: access via `LocalizationService.Instance`. Do not `new` it.
- Lazy-loads translation dictionaries (15 languages). Default is `"zh"` (set in `AppSettings`).
- To add a new string: add the key to **every** language dictionary in `LoadTranslation()`.
- UI elements that bind to localized strings must subscribe to `LocalizationService.Instance.PropertyChanged` and watch for `"Item[]"`.

### Bluetooth / Audio
- `BluetoothService.StartWatching()` fires `Added` in a rapid burst during initial enumeration. Batch UI updates — use `EnumerationCompleted` to signal the end of the flood.
- `AudioService` implements retry logic (3 attempts, 500ms delay) on `OpenAsync`.
- The app uses `Windows.Devices.Enumeration.DeviceWatcher` and `Windows.Media.Audio.AudioPlaybackConnection` — these are WinRT APIs.

### Thread safety
- `BluetoothService` and `AudioService` fire events from arbitrary threads. Subscribers must dispatch to the UI thread.
- Collection binding uses `BindingOperations.EnableCollectionSynchronization` for thread-safe ObservableCollection updates.

### Settings
- Settings file: `%APPDATA%\BluetoothAudioReceiver\settings.json`
- `AppSettings.Load()` returns defaults if the file doesn't exist. Call `.Save()` after changes.
- Crash logs: `%LOCALAPPDATA%\BluetoothAudioReceiver\logs\crash_log.txt` (rotates at 5MB).

### Single instance
- Global mutex `BluetoothAudioReceiver_SingleInstance_Mutex_Global` prevents duplicate processes.
- Second instance wakes the first via `EventWaitHandle` and exits.
- Command-line arg `--minimized` starts the app hidden to tray.

### Tray / minimize-to-tray
- Closing the window hides to tray (not exit) when `MinimizeToTray` is true (default).
- Actual exit happens via the tray context menu "Exit" item.

## NuGet dependencies
- `CommunityToolkit.Mvvm` 8.2.2 — MVVM source generators
- `NAudio` 2.2.1 — audio processing

## Signing scripts
- `scripts/Create-Test-Cert.ps1` — generates a self-signed certificate for local testing
- `scripts/Sign-Build.ps1` — builds and signs with a PFX certificate
