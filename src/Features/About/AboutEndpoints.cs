using System.Diagnostics;
using System.Runtime.InteropServices;
using Atlas.Extensions;
using Atlas.Features.About.Contracts;
using Atlas.Features.Demo;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.About;

public static class AboutEndpoints
{
    /// <summary>GC 配置在进程生命周期内不变，取一次即可</summary>
    private static readonly IReadOnlyDictionary<string, object> GcConfiguration =
        GC.GetConfigurationVariables();

    // 进程启动后不再变化，只探测一次
    private static readonly DateTimeOffset StartedAt = ReadStartedAt();

    public static IEndpointRouteBuilder MapAboutEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/about", GetAbout)
            .WithTags("About")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Ok<AboutResponse>> GetAbout(
        AboutService aboutService,
        IConfiguration configuration,
        AtlasPaths paths,
        DemoMode demo,
        CancellationToken ct)
    {
        var (cardCount, imageCount) = await aboutService.GetCountsAsync(ct);

        return TypedResults.Ok(new AboutResponse(
            AppVersion.Current,
            RuntimeInformation.FrameworkDescription,
            StartedAt,
            GetUptimeSeconds(),
            GetDatabaseBytes(configuration, paths),
            cardCount,
            imageCount,
            // 演示站点不暴露真实部署路径
            demo.Enabled ? string.Empty : paths.DataDirectory,
            ReadMemory()));
    }

    /// <summary>
    ///     读取内存与 GC 运行配置。WorkingSet 用 <see cref="Environment.WorkingSet" />，
    ///     比每次 new Process 少一个对象和一个句柄
    /// </summary>
    private static AboutMemoryInfo ReadMemory()
    {
        var info = GC.GetGCMemoryInfo();

        return new AboutMemoryInfo(
            Environment.WorkingSet,
            GC.GetTotalMemory(false),
            info.TotalCommittedBytes,
            info.FragmentedBytes,
            ReadGcFlag("ServerGC"),
            ReadGcFlag("ConcurrentGC"),
            (int)ReadGcNumber("GCConserveMem"),
            (int)ReadGcNumber("HeapCount"));
    }

    /// <summary>键名来自 GC.GetConfigurationVariables，缺失时按 false 处理</summary>
    private static bool ReadGcFlag(string key)
    {
        return GcConfiguration.TryGetValue(key, out var value) && value is true;
    }

    private static long ReadGcNumber(string key)
    {
        return GcConfiguration.TryGetValue(key, out var value) && value is long number
            ? number
            : 0;
    }

    private static long GetUptimeSeconds()
    {
        return Math.Max(
            0, (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds);
    }

    private static DateTimeOffset ReadStartedAt()
    {
        try
        {
            return new DateTimeOffset(Process.GetCurrentProcess().StartTime);
        }
        catch (Exception)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    /// <summary>从连接字符串解析 SQLite 文件路径并读取大小，无法解析时返回 0</summary>
    private static long GetDatabaseBytes(
        IConfiguration configuration, AtlasPaths paths)
    {
        // 与 FreeSql 使用同一套解析：相对路径按数据目录绝对化，避免统计到另一个文件
        var path = DatabaseConnection.GetFilePath(
            configuration.GetConnectionString("Atlas"), paths.DataDirectory);

        if (string.IsNullOrEmpty(path)) return 0;

        try
        {
            var file = new FileInfo(path);
            return file.Exists ? file.Length : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}