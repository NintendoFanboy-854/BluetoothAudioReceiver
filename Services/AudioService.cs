using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Audio;

namespace BluetoothAudioReceiver.Services;

public class AudioService : IDisposable
{
    private readonly Dictionary<string, AudioPlaybackConnection> _enabledConnections = new();
    private AudioPlaybackConnection? _activeConnection;
    private string? _activeDeviceId;
    private readonly object _lock = new();
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? StreamingStateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _activeConnection != null;
    public bool IsStreaming { get; private set; }
    public string? ActiveDeviceId => _activeDeviceId;

    public bool Enable(string deviceId)
    {
        lock (_lock)
        {
            if (_enabledConnections.ContainsKey(deviceId))
                return true;
        }

        var connection = AudioPlaybackConnection.TryCreateFromId(deviceId);
        if (connection == null)
            return false;

        connection.StateChanged += OnConnectionStateChanged;

        try
        {
            connection.Start();
        }
        catch (Exception ex)
        {
            connection.StateChanged -= OnConnectionStateChanged;
            ErrorOccurred?.Invoke(this, $"Failed to enable device: {ex.Message}");
            return false;
        }

        lock (_lock)
        {
            _enabledConnections[deviceId] = connection;
        }
        return true;
    }

    public async Task<bool> OpenAsync(string deviceId)
    {
        AudioPlaybackConnection? connection;
        lock (_lock)
        {
            if (!_enabledConnections.TryGetValue(deviceId, out connection))
                return false;
        }

        await CloseAsync();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await connection.OpenAsync();
                if (result.Status == AudioPlaybackConnectionOpenResultStatus.Success)
                {
                    await WaitForStateAsync(connection, AudioPlaybackConnectionState.Opened, 500);

                    _activeConnection = connection;
                    _activeDeviceId = deviceId;
                    IsStreaming = connection.State == AudioPlaybackConnectionState.Opened;
                    StreamingStateChanged?.Invoke(this, IsStreaming ? "Streaming" : "Connected");
                    ConnectionStateChanged?.Invoke(this, true);
                    return true;
                }

                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelayMs);
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                {
                    ErrorOccurred?.Invoke(this, ex.Message);
                    return false;
                }
                await Task.Delay(RetryDelayMs);
            }
        }

        ErrorOccurred?.Invoke(this, "Could not establish connection after multiple attempts.");
        return false;
    }

    public async Task CloseAsync()
    {
        AudioPlaybackConnection? old;
        bool wasConnected;
        lock (_lock)
        {
            old = _activeConnection;
            wasConnected = old != null;
            _activeConnection = null;
            var deviceId = _activeDeviceId;
            _activeDeviceId = null;
            if (deviceId != null)
                _enabledConnections.Remove(deviceId);
        }

        if (old != null)
        {
            try
            {
                old.StateChanged -= OnConnectionStateChanged;
                old.Dispose();
            }
            catch
            {
            }
        }

        IsStreaming = false;
        if (wasConnected)
            ConnectionStateChanged?.Invoke(this, false);

        await Task.CompletedTask;
    }

    private async Task WaitForStateAsync(AudioPlaybackConnection connection, AudioPlaybackConnectionState targetState, int timeoutMs)
    {
        if (connection.State == targetState)
            return;

        var tcs = new TaskCompletionSource<bool>();

        TypedEventHandler<AudioPlaybackConnection, object> handler = (sender, args) =>
        {
            if (sender.State == targetState)
                tcs.TrySetResult(true);
        };

        connection.StateChanged += handler;

        try
        {
            if (connection.State == targetState)
                return;

            var timeoutTask = Task.Delay(timeoutMs);
            await Task.WhenAny(tcs.Task, timeoutTask);
        }
        finally
        {
            connection.StateChanged -= handler;
        }
    }

    private void OnConnectionStateChanged(AudioPlaybackConnection sender, object args)
    {
        var wasStreaming = IsStreaming;
        IsStreaming = sender.State == AudioPlaybackConnectionState.Opened;

        if (wasStreaming != IsStreaming)
        {
            StreamingStateChanged?.Invoke(this, IsStreaming ? "Streaming" : "Connected");
        }

        if (sender.State == AudioPlaybackConnectionState.Closed && sender == _activeConnection)
        {
            IsStreaming = false;
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_activeConnection != null)
            {
                try
                {
                    _activeConnection.StateChanged -= OnConnectionStateChanged;
                    _activeConnection.Dispose();
                }
                catch
                {
                }
                _activeConnection = null;
            }

            foreach (var kv in _enabledConnections)
            {
                try
                {
                    kv.Value.StateChanged -= OnConnectionStateChanged;
                    kv.Value.Dispose();
                }
                catch
                {
                }
            }
            _enabledConnections.Clear();
        }
        _activeDeviceId = null;
        IsStreaming = false;
    }
}
