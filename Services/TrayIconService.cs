using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows;
using BluetoothAudioReceiver.Models;

namespace BluetoothAudioReceiver.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private readonly Window _mainWindow;
    private readonly AppSettings _settings;
    private ToolStripMenuItem? _showItem;
    private ToolStripMenuItem? _settingsItem;
    private ToolStripMenuItem? _exitItem;
    
    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;
    
    public TrayIconService(Window mainWindow, AppSettings settings)
    {
        _mainWindow = mainWindow;
        _settings = settings;
        InitializeTrayIcon();
    }
    
    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = true,
            Text = "Bluetooth Audio Receiver"
        };
        
        var contextMenu = new ContextMenuStrip();
        
        _showItem = new ToolStripMenuItem(LocalizationService.Instance.Get("Show"));
        _showItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _showItem.Font = new Font(_showItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(_showItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        _settingsItem = new ToolStripMenuItem(LocalizationService.Instance.Get("Settings"));
        _settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_settingsItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        _exitItem = new ToolStripMenuItem(LocalizationService.Instance.Get("Exit"));
        _exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_exitItem);
        
        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void RefreshContextMenu()
    {
        if (_showItem != null) _showItem.Text = LocalizationService.Instance.Get("Show");
        if (_settingsItem != null) _settingsItem.Text = LocalizationService.Instance.Get("Settings");
        if (_exitItem != null) _exitItem.Text = LocalizationService.Instance.Get("Exit");
    }
    
    private Icon CreateIcon()
    {
        var uri = new Uri("pack://application:,,,/logo.png");
        var streamInfo = System.Windows.Application.GetResourceStream(uri);
        using var bitmap = new Bitmap(streamInfo.Stream);
        return Icon.FromHandle(bitmap.GetHicon());
    }
    
    public void UpdateStatus(string status)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Text = $"Bluetooth Audio Receiver - {status}";
        }
    }
    
    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_trayIcon != null && _settings.ShowNotifications)
        {
            _trayIcon.ShowBalloonTip(3000, title, message, icon);
        }
    }
    
    public void Dispose()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
