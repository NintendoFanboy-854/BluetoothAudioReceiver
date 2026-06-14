using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioReceiver.Models;
using BluetoothAudioReceiver.Services;

namespace BluetoothAudioReceiver;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AppSettings _originalSettings;
    private readonly VolumeService _volumeService;
    private readonly LocalizationService _loc;
    
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        
        _settings = settings;
        _volumeService = new VolumeService();
        _loc = LocalizationService.Instance;
        
        _loc.CurrentLanguage = settings.Language;
        
        _settings.Volume = _volumeService.GetVolume();
        
        _originalSettings = new AppSettings
        {
            Volume = settings.Volume,
            AutoStart = settings.AutoStart,
            StartMinimized = settings.StartMinimized,
            MinimizeToTray = settings.MinimizeToTray,
            AutoConnect = settings.AutoConnect,
            ShowNotifications = settings.ShowNotifications,
            Language = settings.Language
        };
        
        DataContext = _settings;
        
        LanguageComboBox.ItemsSource = LocalizationService.AvailableLanguages;
        LanguageComboBox.SelectedValue = _settings.Language;
        
        VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
        
        ApplyLocalization();
    }
    
    private void ApplyLocalization()
    {
        Title = _loc["Settings"];
        SettingsTitleBarText.Text = _loc["Settings"];
        
        SettingsCloseButton.ToolTip = _loc["Close"];
        AutomationProperties.SetName(SettingsCloseButton, _loc["Close"]);
        
        VolumeHeader.Text = _loc["Volume"];
        AutostartHeader.Text = _loc["Autostart"];
        BehaviorHeader.Text = _loc["Behavior"];
        LanguageHeader.Text = _loc["Language"];
        
        AutomationProperties.SetName(VolumeSlider, _loc["Volume"]);
        AutomationProperties.SetName(LanguageComboBox, _loc["Language"]);

        AutoStartCheckBox.Content = _loc["StartWithWindows"];
        StartMinimizedCheckBox.Content = _loc["StartMinimized"];
        MinimizeToTrayCheckBox.Content = _loc["MinimizeToTray"];
        AutoConnectCheckBox.Content = _loc["AutoConnect"];
        ShowNotificationsCheckBox.Content = _loc["ShowNotifications"];
        
        SaveButton.Content = _loc["Save"];
        CancelButton.Content = _loc["Cancel"];
    }
    
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _volumeService.SetVolume((int)e.NewValue);
    }
    
    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedValue is string langCode)
        {
            _settings.Language = langCode;
            _loc.CurrentLanguage = langCode;
            ApplyLocalization();
        }
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.AutoStart != _originalSettings.AutoStart || 
            _settings.StartMinimized != _originalSettings.StartMinimized)
        {
            AutoStartService.SetAutoStart(_settings.AutoStart, _settings.StartMinimized);
        }
        
        _volumeService.SetVolume(_settings.Volume);
        
        _settings.Save();
        
        _volumeService.Dispose();
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _volumeService.SetVolume(_originalSettings.Volume);
        
        _loc.CurrentLanguage = _originalSettings.Language;
        
        _settings.Volume = _originalSettings.Volume;
        _settings.AutoStart = _originalSettings.AutoStart;
        _settings.StartMinimized = _originalSettings.StartMinimized;
        _settings.MinimizeToTray = _originalSettings.MinimizeToTray;
        _settings.AutoConnect = _originalSettings.AutoConnect;
        _settings.ShowNotifications = _originalSettings.ShowNotifications;
        _settings.Language = _originalSettings.Language;
        
        _volumeService.Dispose();
        DialogResult = false;
        Close();
    }
}
