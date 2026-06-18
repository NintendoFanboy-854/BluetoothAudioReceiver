using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using BluetoothAudioReceiver.Models;
using BluetoothAudioReceiver.Services;
using BluetoothAudioReceiver.ViewModels;

namespace BluetoothAudioReceiver;

public partial class MainWindow : Window
{
    private TrayIconService? _trayService;
    private AppSettings _settings;
    private MainViewModel? _viewModel;
    private bool _isExiting;
    
    public MainWindow()
    {
        InitializeComponent();

        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/logo.png"));
        
        _settings = AppSettings.Load();
        
        LocalizationService.Instance.CurrentLanguage = _settings.Language;
        
        _viewModel = new MainViewModel(_settings);
        DataContext = _viewModel;
        
        _trayService = new TrayIconService(this, _settings);
        _trayService.ShowWindowRequested += OnShowWindowRequested;
        _trayService.ExitRequested += OnExitRequested;
        _trayService.SettingsRequested += OnSettingsRequested;
        
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        StateChanged += OnStateChanged;
        Closing += OnClosing;

        ApplyLocalization();
        
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--minimized") || _settings.StartMinimized)
        {
            WindowState = WindowState.Minimized;
            if (_settings.MinimizeToTray)
            {
                Hide();
            }
        }
    }
    
    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        
        Title = loc.Get("AppTitle");
        TitleBarText.Text = loc.Get("AppTitle");
        
        MinimizeButton.ToolTip = loc.Get("Minimized");
        AutomationProperties.SetName(MinimizeButton, loc.Get("Minimized"));
        CloseButton.ToolTip = loc.Get("Close");
        AutomationProperties.SetName(CloseButton, loc.Get("Close"));
        
        HelpButton.ToolTip = loc.Get("Help");
        AutomationProperties.SetName(HelpButton, loc.Get("Help"));
        SettingsButton.ToolTip = loc.Get("Settings");
        AutomationProperties.SetName(SettingsButton, loc.Get("Settings"));
        
        DevicesHeader.Text = loc.Get("Devices");
        
        RefreshButton.ToolTip = loc.Get("Refresh");
        AutomationProperties.SetName(RefreshButton, loc.Get("Refresh"));
        
        EmptyStateText.Text = loc.Get("NoDevicesFound");
        
        ConnectButton.Content = loc.Get("Connect");
        DisconnectButton.Content = loc.Get("Disconnect");
        
        RebindSelectedDeviceFallbacks(loc);
        
        UpdateDefaultDeviceTooltip();
    }
    
    private void UpdateDefaultDeviceTooltip()
    {
        var loc = LocalizationService.Instance;
        var isDefault = _viewModel?.IsDefaultDevice ?? false;
        DefaultDeviceButton.ToolTip = isDefault ? loc.Get("RemoveDefault") : loc.Get("SetDefault");
        AutomationProperties.SetName(DefaultDeviceButton, isDefault ? loc.Get("RemoveDefault") : loc.Get("SetDefault"));
    }
    
    private void RebindSelectedDeviceFallbacks(LocalizationService loc)
    {
        var nameBinding = new Binding("SelectedDevice.Name")
        {
            FallbackValue = loc.Get("NoDeviceSelected")
        };
        SelectedDeviceName.SetBinding(TextBlock.TextProperty, nameBinding);
        
        var statusBinding = new Binding("SelectedDevice.DisplayStatus")
        {
            FallbackValue = loc.Get("SelectFromList")
        };
        SelectedDeviceStatus.SetBinding(TextBlock.TextProperty, statusBinding);
    }
    
    #region Custom Title Bar
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.MinimizeToTray)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _trayService?.ShowNotification(LocalizationService.Instance.Get("Minimized"), LocalizationService.Instance.Get("AppRunningInTray"));
        }
        else
        {
            _isExiting = true;
            Close();
        }
    }
    
    #endregion
    
    #region ViewModel Events
    
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null || _trayService == null) return;
        
        if (e.PropertyName == nameof(MainViewModel.Status))
        {
            _trayService.UpdateStatus(_viewModel.Status);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsConnected))
        {
            if (_viewModel.IsConnected)
            {
                _trayService.ShowNotification(LocalizationService.Instance.Get("Connected"), 
                    $"{LocalizationService.Instance.Get("AudioConnectionTo")} {_viewModel.SelectedDevice?.Name} {LocalizationService.Instance.Get("Established")}");
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsDefaultDevice))
        {
            UpdateDefaultDeviceTooltip();
        }
    }
    
    #endregion
    
    #region Window State
    
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            Hide();
        }
    }
    
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            _trayService?.ShowNotification(LocalizationService.Instance.Get("Minimized"), 
                LocalizationService.Instance.Get("AppRunningInTray"));
        }
        else
        {
            _viewModel?.Dispose();
            _trayService?.Dispose();
        }
    }
    
    #endregion
    
    #region Tray Events
    
    private void OnShowWindowRequested(object? sender, EventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    
    private void OnExitRequested(object? sender, EventArgs e)
    {
        _isExiting = true;
        _viewModel?.Dispose();
        _trayService?.Dispose();
        Application.Current.Shutdown();
    }
    
    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        ShowSettingsWindow();
    }
    
    #endregion
    
    #region Button Handlers
    
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }
    
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow { Owner = this };
        helpWindow.ShowDialog();
    }
    
    private void ShowSettingsWindow()
    {
        if (!IsVisible)
        {
            Show();
            WindowState = WindowState.Normal;
        }
        
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            _settings = AppSettings.Load();
            LocalizationService.Instance.CurrentLanguage = _settings.Language;
            ApplyLocalization();
            _viewModel?.RefreshLocalization();
            _trayService?.RefreshContextMenu();
        }
    }
    
    #endregion
}

public class CanConnectConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return false;
        
        var hasSelectedDevice = values[0] != null;
        var isConnected = values[1] is bool connected && connected;
        var isConnecting = values[2] is bool connecting && connecting;
        
        return hasSelectedDevice && !isConnected && !isConnecting;
    }
    
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
