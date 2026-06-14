using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using BluetoothAudioReceiver.Services;

namespace BluetoothAudioReceiver.Models;

public partial class BluetoothDevice : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private bool _isAudioStreaming;

    public string DisplayStatus => IsAudioStreaming
        ? LocalizationService.Instance.Get("Streaming")
        : (IsConnected
            ? LocalizationService.Instance.Get("Connected")
            : LocalizationService.Instance.Get("Paired"));

    public BluetoothDevice()
    {
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
        {
            OnPropertyChanged(nameof(DisplayStatus));
        }
    }
}
