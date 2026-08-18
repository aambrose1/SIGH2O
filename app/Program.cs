using Microsoft.EntityFrameworkCore;
using app.Components;
using app.Data;
using app.Services;
using app.WebSockets;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddSingleton<SensorService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddHostedService<WatchdogService>();
builder.Services.AddScoped<WebSocketHandler>();

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
};

forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseAntiforgery();

// ── Run EF migrations & seed state service ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var state = app.Services.GetRequiredService<SensorService>();
    var history = await db.SensorLogs
        .OrderByDescending(l => l.TimestampUtc)
        .Take(200)
        .OrderBy(l => l.TimestampUtc)
        .ToListAsync();
    state.Seed(history);
}

// ── WebSocket endpoint (/ws) ──────────────────────────────────────────────────
app.UseWebSockets();
app.Map("/ws", async context =>
{
	var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
	logger.LogInformation("WS request. IsWebSocket: {IsWs}, Headers: {Headers}",
        context.WebSockets.IsWebSocketRequest,
        string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket  = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleAsync(socket, context.RequestAborted);
});

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
