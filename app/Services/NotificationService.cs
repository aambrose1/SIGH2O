namespace app.Services;

public class NotificationService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    // Prevent alert spam: don't fire again within this window after the last alert
    private DateTime _lastAlertSent = DateTime.MinValue;
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromHours(4);

    public NotificationService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendLowMoistureAlertAsync(double moisturePercent)
    {
        if (DateTime.UtcNow - _lastAlertSent < CooldownPeriod)
        {
            _logger.LogInformation("Alert suppressed — still within cooldown window.");
            return;
        }

        var pushoverToken = _config["Pushover:ApiToken"];
        var pushoverUser  = _config["Pushover:UserKey"];

        if (string.IsNullOrEmpty(pushoverToken) || string.IsNullOrEmpty(pushoverUser))
        {
            _logger.LogWarning("Pushover credentials not configured. Skipping alert.");
            return;
        }

        var client = _httpFactory.CreateClient();
        var payload = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token",   pushoverToken),
            new KeyValuePair<string, string>("user",    pushoverUser),
            new KeyValuePair<string, string>("title",   "⚠ Low Soil Moisture"),
            new KeyValuePair<string, string>("message",
                $"Moisture dropped to {moisturePercent:F1}% — your plant needs water."),
            new KeyValuePair<string, string>("priority", "1"),
        });

        try
        {
            var response = await client.PostAsync("https://api.pushover.net/1/messages.json", payload);
            response.EnsureSuccessStatusCode();
            _lastAlertSent = DateTime.UtcNow;
            _logger.LogInformation("Low-moisture alert sent at {Time}.", _lastAlertSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Pushover notification.");
        }
    }

    private DateTime _lastOfflineAlertSent = DateTime.MinValue;

    public async Task SendDeviceOfflineAlertAsync()
    {
        if (DateTime.UtcNow - _lastOfflineAlertSent < CooldownPeriod)
        {
            _logger.LogInformation("Offline alert suppressed — still within cooldown window.");
            return;
        }

        var pushoverToken = _config["Pushover:ApiToken"];
        var pushoverUser  = _config["Pushover:UserKey"];

        if (string.IsNullOrEmpty(pushoverToken) || string.IsNullOrEmpty(pushoverUser))
        {
            _logger.LogWarning("Pushover credentials not configured. Skipping alert.");
            return;
        }

        var client = _httpFactory.CreateClient();
        var payload = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token",   pushoverToken),
            new KeyValuePair<string, string>("user",    pushoverUser),
            new KeyValuePair<string, string>("title",   "⚠ Soil Monitor Offline"),
            new KeyValuePair<string, string>("message", "No reading received in over 5 hours — check your device."),
            new KeyValuePair<string, string>("priority", "1"),
        });

        try
        {
            var response = await client.PostAsync("https://api.pushover.net/1/messages.json", payload);
            response.EnsureSuccessStatusCode();
            _lastOfflineAlertSent = DateTime.UtcNow;
            _logger.LogInformation("Device offline alert sent at {Time}.", _lastOfflineAlertSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send offline Pushover notification.");
        }
    }
}