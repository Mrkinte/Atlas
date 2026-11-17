using System.Net.WebSockets;
using System.Text.Json;
using Atlas.Features.Demo;
using Atlas.Features.Settings;
using Atlas.Features.Settings.Contracts;
using Atlas.Features.SystemStatus.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.SystemStatus;

/// <summary>
///     系统状态 WebSocket。端点允许匿名连接，但未登录连接只会收到公开硬件白名单内的设备，
///     且不包含硬件 ID 与真实主机名
/// </summary>
public static class SystemStatusEndpoints
{
    public const string WsPath = "/api/system-status/ws";

    private const string DeniedReason = "hardware-not-public";

    private const string DisabledReason = "system-status-disabled";

    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapSystemStatusEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system-status")
            .WithTags("SystemStatus");

        group.MapGet("/hardware", GetHardware).RequireAuthorization();
        group.MapGet("/ws", WebSocket).AllowAnonymous();

        return endpoints;
    }

    private static Ok<SystemStatusHardware> GetHardware(
        SystemStatusSampler sampler, DemoMode demo)
    {
        return TypedResults.Ok(demo.Enabled
            ? new SystemStatusHardware(string.Empty, "演示设备")
            : new SystemStatusHardware(sampler.HardwareId, sampler.HardwareName));
    }

    private static async Task WebSocket(
        HttpContext context,
        SystemStatusSampler sampler,
        SettingsService settingsService,
        SystemStatusConnectionLimiter limiter,
        SystemStatusRuntime runtime,
        DemoMode demo)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket request.");
            return;
        }

        if (!IsAllowedOrigin(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Origin not allowed.");
            return;
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        using var lease = limiter.TryAcquire(clientKey);
        if (lease is null)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many WebSocket connections.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var ct = context.RequestAborted;

        // 演示账号由服务端自动登录，按匿名连接处理才不会看到真实主机信息
        var isAuthenticated = !demo.Enabled && context.User.Identity?.IsAuthenticated == true;

        SettingsResponse settings;
        try
        {
            settings = await settingsService.GetAsync(ct);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // 客户端在查询配置期间断开
            return;
        }

        // 以数据库配置为准同步运行期状态，避免重启或恢复备份后出现偏差
        var enabled = settings.SystemStatusEnabled;
        runtime.SetEnabled(enabled);

        try
        {
            if (!enabled)
            {
                await SendDisabledAndCloseAsync(socket, ct);
                return;
            }

            var canViewHardware = isAuthenticated
                                  || settings.PublicHardware.Contains(
                                      "*", StringComparer.Ordinal)
                                  || settings.PublicHardware.Contains(
                                      sampler.HardwareId, StringComparer.Ordinal);

            if (!canViewHardware)
            {
                await SendAsync(socket, new SystemStatusMessage(
                    "denied", DateTimeOffset.UtcNow, [], DeniedReason), ct);
                await socket.CloseOutputAsync(
                    WebSocketCloseStatus.PolicyViolation, DeniedReason, ct);
                return;
            }

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                // 设置页关闭开关后，已建立的连接在下一轮推送时主动断开
                if (!runtime.Enabled)
                {
                    await SendDisabledAndCloseAsync(socket, ct);
                    return;
                }

                var device = sampler.Sample();
                if (!isAuthenticated)
                    // 匿名连接只发送聚合指标，隐藏硬件 ID 与主机名
                    device = device with { HardwareId = string.Empty, HardwareName = "本机" };

                await SendAsync(socket, new SystemStatusMessage(
                    "status", DateTimeOffset.UtcNow, [device], null), ct);

                await Task.Delay(PushInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                try
                {
                    await socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
        }
    }

    private static async Task SendDisabledAndCloseAsync(
        WebSocket socket, CancellationToken ct)
    {
        await SendAsync(socket, new SystemStatusMessage(
            "disabled", DateTimeOffset.UtcNow, [], DisabledReason), ct);
        await socket.CloseOutputAsync(
            WebSocketCloseStatus.PolicyViolation, DisabledReason, ct);
    }

    /// <summary>
    ///     浏览器发起的 WebSocket 一定带 Origin；非浏览器客户端可能不带，此时放行，
    ///     匿名连接仍受硬件白名单限制。带 Origin 时必须与当前请求同源
    /// </summary>
    private static bool IsAllowedOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        var requestScheme = context.Request.IsHttps ? "https" : "http";
        return string.Equals(uri.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   uri.Authority, context.Request.Host.Value,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SendAsync(
        WebSocket socket,
        SystemStatusMessage message,
        CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            message, SystemStatusJsonContext.Default.SystemStatusMessage);
        await socket.SendAsync(
            payload, WebSocketMessageType.Text, true, ct);
    }
}