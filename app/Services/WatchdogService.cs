using app.Models;

namespace app.Services;
public class WatchdogService : BackgroundService
{
    private readonly SensorService _state;
    private readonly NotificationService _notifications;

    public WatchdogService(SensorService state, NotificationService notifications)
    {
        _state         = state;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), ct); // Check every hour

            if (_state.Latest is not null &&
                DateTime.UtcNow - _state.Latest.TimestampUtc > TimeSpan.FromHours(5)) // Alert if no reading in 5+ hours
            {
                await _notifications.SendDeviceOfflineAlertAsync();
            }
        }
    }
}