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

        var ntfyTopic = _config["Ntfy:Topic"];

        if (string.IsNullOrEmpty(ntfyTopic))
        {
            _logger.LogWarning("Ntfy topic not configured. Skipping alert.");
            return;
        }

        var ntfyServer = _config["Ntfy:ServerUrl"] ?? "https://ntfy.sh";
        var client = _httpFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ntfyServer}/{ntfyTopic}")
        {
            Content = new StringContent($"Moisture dropped to {moisturePercent:F1}% — your plant needs water.")
        };
        request.Headers.Add("Title", "Low Soil Moisture");
        request.Headers.Add("Priority", "high");
        request.Headers.Add("Tags", "warning,droplet");

        try
        {
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _lastAlertSent = DateTime.UtcNow;
            _logger.LogInformation("Low-moisture alert sent at {Time}.", _lastAlertSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ntfy notification.");
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

        var ntfyTopic = _config["Ntfy:Topic"];

        if (string.IsNullOrEmpty(ntfyTopic))
        {
            _logger.LogWarning("Ntfy topic not configured. Skipping alert.");
            return;
        }

        var ntfyServer = _config["Ntfy:ServerUrl"] ?? "https://ntfy.sh";
        var client = _httpFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ntfyServer}/{ntfyTopic}")
        {
            Content = new StringContent("No reading received in over 5 hours — check your device.")
        };
        request.Headers.Add("Title", "Soil Monitor Offline");
        request.Headers.Add("Priority", "high");
        request.Headers.Add("Tags", "warning,satellite");

        try
        {
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _lastOfflineAlertSent = DateTime.UtcNow;
            _logger.LogInformation("Device offline alert sent at {Time}.", _lastOfflineAlertSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send offline ntfy notification.");
        }
    }
}