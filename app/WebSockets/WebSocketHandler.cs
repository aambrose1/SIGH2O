using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using app.Data;
using app.Models;
using app.Services;

namespace app.WebSockets;

public class WebSocketHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SensorService _state;
    private readonly NotificationService _notifications;
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(
        IServiceScopeFactory scopeFactory,
        SensorService state,
        NotificationService notifications,
        ILogger<WebSocketHandler> logger)
    {
        _scopeFactory    = scopeFactory;
        _state           = state;
        _notifications   = notifications;
        _logger          = logger;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        _logger.LogInformation("ESP32 connected.");
        var buffer = new byte[256];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var raw = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
                _logger.LogDebug("Received payload: {Payload}", raw);

                if (!TryParsePayload(raw, out double moisture))
                {
                    _logger.LogWarning("Unparseable payload: {Raw}", raw);
                    continue;
                }

                var log = new SensorLogs
                {
                    MoistureValue = moisture,
                    TimestampUtc    = DateTime.UtcNow,
                };

                // Persist to PostgreSQL
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.SensorLogs.Add(log);
                await db.SaveChangesAsync(ct);

                // Push to live UI
                _state.Push(log);

                // Fire alert if below threshold
                if (_state.IsAlert)
                    await _notifications.SendLowMoistureAlertAsync(moisture);
            }
        }
        catch (OperationCanceledException) { /* server shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error.");
        }
        finally
        {
            _logger.LogInformation("ESP32 disconnected.");
        }
    }

    // Accepts either a bare number ("73.4") or a JSON object ({"moisture":73.4})
    private static bool TryParsePayload(string raw, out double moisture)
    {
        if (double.TryParse(raw, out moisture))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("moisture", out var el))
            {
                moisture = el.GetDouble();
                return true;
            }
        }
        catch { /* fall through */ }

        moisture = 0;
        return false;
    }
}