using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BluetoothAudioReceiver.Models;
using BluetoothAudioReceiver.Services;

namespace BluetoothAudioReceiver.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly BluetoothService _bluetoothService;
    private readonly AudioService _audioService;
    private readonly object _devicesLock = new();
    private bool _isEnumerating = true;
    private string _streamingState = "";
    private AppSettings _settings;
    private string? _pendingAutoConnectId;

    [ObservableProperty]
    private ObservableCollection<BluetoothDevice> _devices = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultDevice))]
    private BluetoothDevice? _selectedDevice;

    [ObservableProperty]
    private string _status = LocalizationService.Instance.Get("Idle");

    [ObservableProperty]
    private string _statusDetail = LocalizationService.Instance.Get("SelectDevice");

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsDefaultDevice => SelectedDevice != null
        && _settings.DefaultDeviceId == SelectedDevice.Id;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        _bluetoothService = new BluetoothService();
        _audioService = new AudioService();

        BindingOperations.EnableCollectionSynchronization(Devices, _devicesLock);

        _bluetoothService.DeviceAdded += OnDeviceAdded;
        _bluetoothService.DeviceRemoved += OnDeviceRemoved;
        _bluetoothService.EnumerationCompleted += OnEnumerationCompleted;

        _audioService.ConnectionStateChanged += OnAudioConnectionStateChanged;
        _audioService.StreamingStateChanged += OnStreamingStateChanged;
        _audioService.ErrorOccurred += OnAudioError;

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;

        _bluetoothService.StartWatching();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
        {
            RefreshLocalization();
        }
    }

    public void RefreshLocalization()
    {
        if (IsConnected)
        {
            if (_streamingState == "Streaming")
            {
                Status = LocalizationService.Instance.Get("Streaming");
                StatusDetail = string.Format(LocalizationService.Instance.Get("ReceivingAudioFrom"), SelectedDevice?.Name);
            }
            else
            {
                Status = LocalizationService.Instance.Get("Connected");
                StatusDetail = string.Format(LocalizationService.Instance.Get("AudioReadyFrom"), SelectedDevice?.Name);
            }
        }
        else if (IsConnecting)
        {
            Status = LocalizationService.Instance.Get("Connecting");
            StatusDetail = string.Format(LocalizationService.Instance.Get("OpeningConnectionTo"), SelectedDevice?.Name);
        }
        else
        {
            Status = LocalizationService.Instance.Get("Idle");
            if (_isEnumerating)
            {
                StatusDetail = LocalizationService.Instance.Get("Scanning");
            }
            else
            {
                StatusDetail = string.Format(LocalizationService.Instance.Get("DevicesFound"), Devices.Count);
            }
        }
        OnPropertyChanged(nameof(IsDefaultDevice));
    }

    private void OnDeviceAdded(object? sender, BluetoothDevice device)
    {
        if (_settings.DeviceLastConnectedTimes.TryGetValue(device.Id, out var time))
        {
            device.LastConnectedTime = time;
        }

        lock (_devicesLock)
        {
            Devices.Add(device);
            if (!_isEnumerating)
            {
                StatusDetail = string.Format(LocalizationService.Instance.Get("DevicesFound"), Devices.Count);
            }
        }

        if (_pendingAutoConnectId != null && device.Id == _pendingAutoConnectId)
        {
            _pendingAutoConnectId = null;
            _ = AutoConnectToDevice(device);
        }
    }

    private void OnEnumerationCompleted(object? sender, EventArgs e)
    {
        lock (_devicesLock)
        {
            _isEnumerating = false;
            StatusDetail = string.Format(LocalizationService.Instance.Get("DevicesFound"), Devices.Count);
        }

        if (_settings.AutoConnect)
        {
            var targetId = _settings.DefaultDeviceId ?? _settings.LastDeviceId;
            if (targetId != null)
            {
                BluetoothDevice? target = null;
                foreach (var d in Devices)
                {
                    if (d.Id == targetId)
                    {
                        target = d;
                        break;
                    }
                }
                if (target != null)
                {
                    _ = AutoConnectToDevice(target);
                }
                else
                {
                    _pendingAutoConnectId = targetId;
                }
            }
        }
    }

    private async Task AutoConnectToDevice(BluetoothDevice device)
    {
        if (IsConnected || IsConnecting) return;

        SelectedDevice = device;
        IsConnecting = true;
        Status = LocalizationService.Instance.Get("Connecting");
        StatusDetail = string.Format(LocalizationService.Instance.Get("OpeningConnectionTo"), device.Name);
        ErrorMessage = null;

        if (!_audioService.Enable(device.Id))
        {
            ErrorMessage = string.Format("Could not enable audio for {0}", device.Name);
            IsConnecting = false;
            return;
        }

        await _audioService.OpenAsync(device.Id);
    }

    private void OnDeviceRemoved(object? sender, string deviceId)
    {
        lock (_devicesLock)
        {
            for (int i = Devices.Count - 1; i >= 0; i--)
            {
                if (Devices[i].Id == deviceId)
                {
                    Devices.RemoveAt(i);
                    break;
                }
            }
            StatusDetail = string.Format(LocalizationService.Instance.Get("DevicesFound"), Devices.Count);
        }
    }

    private void OnAudioConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        IsConnecting = false;
        _streamingState = "";

        if (connected)
        {
            Status = LocalizationService.Instance.Get("Connected");
            StatusDetail = string.Format(LocalizationService.Instance.Get("AudioReadyFrom"), SelectedDevice?.Name);
            ErrorMessage = null;

            if (SelectedDevice != null)
            {
                SelectedDevice.LastConnectedTime = DateTime.Now;
                _settings.LastDeviceId = SelectedDevice.Id;
                _settings.LastDeviceName = SelectedDevice.Name;
                _settings.LastConnectedTime = DateTime.Now;
                _settings.DeviceLastConnectedTimes[SelectedDevice.Id] = DateTime.Now;
                _settings.Save();
            }
        }
        else
        {
            Status = LocalizationService.Instance.Get("Idle");
            StatusDetail = LocalizationService.Instance.Get("SelectDevice");
        }
    }

    private void OnStreamingStateChanged(object? sender, string state)
    {
        _streamingState = state;
        Status = LocalizationService.Instance.Get(state);
        if (state == "Streaming")
        {
            StatusDetail = string.Format(LocalizationService.Instance.Get("ReceivingAudioFrom"), SelectedDevice?.Name);
        }
    }

    private void OnAudioError(object? sender, string error)
    {
        ErrorMessage = error;
        IsConnecting = false;
    }

    [RelayCommand]
    private async Task OpenConnectionAsync()
    {
        if (SelectedDevice == null) return;

        IsConnecting = true;
        Status = LocalizationService.Instance.Get("Connecting");
        StatusDetail = string.Format(LocalizationService.Instance.Get("OpeningConnectionTo"), SelectedDevice.Name);
        ErrorMessage = null;

        if (!_audioService.Enable(SelectedDevice.Id))
        {
            ErrorMessage = string.Format("Could not enable audio for {0}", SelectedDevice.Name);
            IsConnecting = false;
            return;
        }

        await _audioService.OpenAsync(SelectedDevice.Id);
    }

    [RelayCommand]
    private async Task CloseConnectionAsync()
    {
        await _audioService.CloseAsync();
    }

    [RelayCommand]
    private void ToggleDefaultDevice()
    {
        if (SelectedDevice == null) return;

        if (_settings.DefaultDeviceId == SelectedDevice.Id)
        {
            _settings.DefaultDeviceId = null;
            _settings.DefaultDeviceName = null;
        }
        else
        {
            _settings.DefaultDeviceId = SelectedDevice.Id;
            _settings.DefaultDeviceName = SelectedDevice.Name;
        }
        _settings.Save();
        OnPropertyChanged(nameof(IsDefaultDevice));
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        lock (_devicesLock)
        {
            Devices.Clear();
        }
        _bluetoothService.StopWatching();

        _isEnumerating = true;
        _pendingAutoConnectId = null;
        StatusDetail = LocalizationService.Instance.Get("Scanning");
        _bluetoothService.StartWatching();
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;

        _bluetoothService.DeviceAdded -= OnDeviceAdded;
        _bluetoothService.DeviceRemoved -= OnDeviceRemoved;
        _bluetoothService.EnumerationCompleted -= OnEnumerationCompleted;

        _audioService.ConnectionStateChanged -= OnAudioConnectionStateChanged;
        _audioService.StreamingStateChanged -= OnStreamingStateChanged;
        _audioService.ErrorOccurred -= OnAudioError;

        _bluetoothService.Dispose();
        _audioService.Dispose();
    }
}
