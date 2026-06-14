using System;
using System.IO;
using System.Threading;
using System.Windows;
using BluetoothAudioReceiver.Services;

namespace BluetoothAudioReceiver;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _wakeEvent;
    private static RegisteredWaitHandle? _wakeWaitHandle;

    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appName = "BluetoothAudioReceiver_SingleInstance_Mutex_Global";
        bool createdNew;

        _mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            try
            {
                using var wakeEvent = EventWaitHandle.OpenExisting("BluetoothAudioReceiver_WakeEvent");
                wakeEvent.Set();
            }
            catch
            {
                MessageBox.Show(LocalizationService.Instance.Get("SingleInstanceMessage"),
                    LocalizationService.Instance.Get("SingleInstanceTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "BluetoothAudioReceiver_WakeEvent");
        _wakeWaitHandle = ThreadPool.RegisterWaitForSingleObject(_wakeEvent, OnWakeSignal, null, Timeout.Infinite, false);

        base.OnStartup(e);
    }

    private static void OnWakeSignal(object? state, bool timedOut)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow != null)
            {
                var window = Application.Current.MainWindow;
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                window.Show();
                window.Activate();
                window.Topmost = true;
                window.Topmost = false;
            }
        });

        if (_wakeEvent != null)
            _wakeWaitHandle = ThreadPool.RegisterWaitForSingleObject(_wakeEvent, OnWakeSignal, null, Timeout.Infinite, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeWaitHandle?.Unregister(null);
        _wakeEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.Handled = true;

        string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BluetoothAudioReceiver", "logs", "crash_log.txt");
        MessageBox.Show(string.Format(LocalizationService.Instance.Get("CrashMessage"), logPath), LocalizationService.Instance.Get("CrashTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
        }
    }

    private void LogCrash(Exception ex)
    {
        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BluetoothAudioReceiver", "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            string logPath = Path.Combine(logDir, "crash_log.txt");

            if (File.Exists(logPath) && new FileInfo(logPath).Length > 5 * 1024 * 1024)
            {
                string backupPath = Path.Combine(logDir, "crash_log.bak");
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(logPath, backupPath);
            }

            string errorMessage = $"[{DateTime.Now}] CRASH REPORT:\n{ex.GetType()}: {ex.Message}\nStack Trace:\n{ex.StackTrace}\n\nInner Exception:\n{ex.InnerException?.Message}\n--------------------------------------------------\n";
            File.AppendAllText(logPath, errorMessage);
        }
        catch
        {
        }
    }
}
