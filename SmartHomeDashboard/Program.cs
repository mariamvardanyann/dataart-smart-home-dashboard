using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection("Monitoring"));
builder.Services.AddHttpClient<DeviceMonitoringService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/dashboard", async (
    DeviceMonitoringService service, CancellationToken ct) =>
    Results.Ok(await service.GetSnapshotAsync(ct)));
app.MapGet("/api/devices", async (
    DeviceMonitoringService service, CancellationToken ct) =>
    Results.Ok((await service.GetSnapshotAsync(ct)).Devices));

app.Run();

public sealed class MonitoringOptions
{
    public List<DeviceEndpoint> Devices { get; set; } = [];
}

public sealed class DeviceEndpoint
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string InfoUrl { get; set; } = "";
}

public sealed class EmulatorDeviceInfo
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public Dictionary<string, string>? Metrics { get; set; }
}

public sealed record MonitoredDevice(
    string Id, string Name, string InfoUrl, bool IsOnline,
    string Address, Dictionary<string, string> Metrics, string? Error);

public sealed record DashboardSnapshot(
    int TotalDevices, int OnlineDevices, int OfflineDevices,
    DateTimeOffset CheckedAt, IReadOnlyList<MonitoredDevice> Devices);

public sealed class DeviceMonitoringService(
    HttpClient client, IOptionsMonitor<MonitoringOptions> settings)
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var probes = settings.CurrentValue.Devices.Select(d => CheckAsync(d, ct));
        var devices = await Task.WhenAll(probes);
        var online = devices.Count(d => d.IsOnline);
        return new DashboardSnapshot(
            devices.Length, online, devices.Length - online,
            DateTimeOffset.UtcNow, devices);
    }

    private async Task<MonitoredDevice> CheckAsync(DeviceEndpoint device, CancellationToken ct)
    {
        string fallbackName = string.IsNullOrWhiteSpace(device.Name)
            ? device.Id : device.Name;

        MonitoredDevice Offline(string reason) =>
            new(device.Id, fallbackName, device.InfoUrl, false,
                "—", new Dictionary<string, string>(), reason);

        // The dashboard is local-only and must not probe arbitrary remote systems.
        if (!Uri.TryCreate(device.InfoUrl, UriKind.Absolute, out var url) ||
            !(url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) || !url.IsLoopback)
            return Offline("Only local HTTP or HTTPS device endpoints are supported.");

        try
        {
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Offline($"HTTP {(int)response.StatusCode}");

            var info = await response.Content.ReadFromJsonAsync<EmulatorDeviceInfo>(
                cancellationToken: ct);
            if (info is null) return Offline("Device returned empty data.");

            return new MonitoredDevice(
                device.Id,
                string.IsNullOrWhiteSpace(info.Name) ? fallbackName : info.Name,
                device.InfoUrl,
                true,
                info.Address ?? "Unknown",
                info.Metrics ?? new Dictionary<string, string>(),
                null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or JsonException or NotSupportedException)
        {
            return Offline("Device unavailable or response invalid.");
        }
    }
}
