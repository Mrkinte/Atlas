using System.Text.Json;
using Atlas.Features.Settings.Contracts;
using Atlas.Features.Settings.Entities;
using Atlas.Features.SystemStatus;

namespace Atlas.Features.Settings;

public class SettingsService(IFreeSql fsql, SystemStatusRuntime systemStatus)
{
    private const int RowId = 1;

    private const string DefaultSiteTitle = "Atlas";

    // 未保存过配置时直接展示
    private const string DefaultFooterText = "Powered by marjin";

    private const int TitleMaxLength = 64;

    private const int ColsMin = 12;

    private const int ColsMax = 24;

    private const int MaxWidthMin = 1028;

    private const int MaxWidthMax = 4096;

    private const int BackgroundImageMaxLength = 1024;

    private const int TextMaxLength = 120;

    private const int BlurMin = 0;

    private const int BlurMax = 40;

    internal const int MaskDefault = 35;

    private const int MaskMin = 0;

    private const int MaskMax = 100;

    private const int PublicHardwareMaxCount = 32;

    private const int PublicHardwareMaxLength = 128;

    private const int Live2DOutfitMaxCount = 32;

    private const int Live2DOutfitIdMaxLength = 64;

    /// <summary>尚未保存过配置时返回默认值</summary>
    public async Task<SettingsResponse> GetAsync(CancellationToken ct = default)
    {
        var entity = await fsql.Select<SettingsEntity>()
            .Where(s => s.Id == RowId)
            .FirstAsync(ct);

        return entity?.ToResponse() ?? new SettingsResponse
        {
            SiteTitle = DefaultSiteTitle,
            DesktopCols = ColsMin,
            DesktopGridContainerMaxWidth = MaxWidthMin,
            DesktopGridFullWidth = false,
            BackgroundImage = string.Empty,
            BackgroundBlur = BlurMin,
            BackgroundMask = MaskDefault,
            PrimaryColor = string.Empty,
            PublicHardware = [],
            SystemStatusEnabled = true,
            DockerEnabled = true,
            DockerSocket = string.Empty,
            SiteSubtitle = string.Empty,
            Favicon = string.Empty,
            ShowHeaderLogo = true,
            FooterText = DefaultFooterText,
            ServiceProbeEnabled = true,
            Live2DEnabled = false,
            Live2DModelPath = string.Empty
        };
    }

    /// <summary>写入站点配置；范围与长度限制统一在服务端做，避免异常数据落库</summary>
    public async Task<SettingsResponse> UpdateAsync(
        UpdateSettingsRequest request, CancellationToken ct = default)
    {
        var entity = new SettingsEntity
        {
            Id = RowId,
            SiteTitle = NormalizeTitle(request.SiteTitle),
            DesktopCols = Math.Clamp(request.DesktopCols, ColsMin, ColsMax),
            DesktopGridContainerMaxWidth =
                Math.Clamp(request.DesktopGridContainerMaxWidth, MaxWidthMin, MaxWidthMax),
            DesktopGridFullWidth = request.DesktopGridFullWidth,
            BackgroundImage = NormalizeBackgroundImage(request.BackgroundImage),
            BackgroundBlur = Math.Clamp(request.BackgroundBlur, BlurMin, BlurMax),
            BackgroundMask = Math.Clamp(request.BackgroundMask, MaskMin, MaskMax),
            PrimaryColor = NormalizeColor(request.PrimaryColor),
            PublicHardware = SerializePublicHardware(
                NormalizePublicHardware(request.PublicHardware)),
            SystemStatusEnabled = request.SystemStatusEnabled,
            DockerEnabled = request.DockerEnabled,
            DockerSocket = NormalizeDockerSocket(request.DockerSocket),
            ServiceProbeEnabled = request.ServiceProbeEnabled,
            Live2DEnabled = request.Live2DEnabled,
            SiteSubtitle = NormalizeText(request.SiteSubtitle),
            Favicon = NormalizeFavicon(request.Favicon),
            ShowHeaderLogo = request.ShowHeaderLogo,
            FooterText = NormalizeText(request.FooterText),
            Live2DModelPath = NormalizeLive2DModelPath(request.Live2DModelPath)
        };

        // 单行配置：存在则更新，不存在则插入
        await fsql.InsertOrUpdate<SettingsEntity>()
            .SetSource(entity)
            .ExecuteAffrowsAsync(ct);

        // 开关需要立即作用于已建立的 WebSocket 连接
        systemStatus.SetEnabled(entity.SystemStatusEnabled);

        return entity.ToResponse();
    }

    /// <summary>模型被删除后调用：站点当前使用的模型位于该目录下时回退为内置模型</summary>
    public async Task ResetLive2DModelPathIfUnderAsync(
        string prefix, CancellationToken ct = default)
    {
        var entity = await fsql.Select<SettingsEntity>()
            .Where(s => s.Id == RowId)
            .FirstAsync(ct);
        if (entity is null) return;

        var path = NormalizeLive2DModelPath(entity.Live2DModelPath);
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

        await fsql.Update<SettingsEntity>()
            .Set(s => s.Live2DModelPath, string.Empty)
            .Where(s => s.Id == RowId)
            .ExecuteAffrowsAsync(ct);
    }

    private static string NormalizeTitle(string? value)
    {
        var title = value?.Trim() ?? string.Empty;
        if (title.Length > TitleMaxLength) title = title[..TitleMaxLength];
        return title.Length == 0 ? DefaultSiteTitle : title;
    }

    private static string NormalizeBackgroundImage(string? value)
    {
        var url = value?.Trim() ?? string.Empty;
        return url.Length > BackgroundImageMaxLength
            ? url[..BackgroundImageMaxLength]
            : url;
    }

    private static string NormalizeText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > TextMaxLength ? text[..TextMaxLength] : text;
    }

    /// <summary>只接受站内相对路径与 http(s) 绝对地址，其余回退为空</summary>
    private static string NormalizeFavicon(string? value)
    {
        var url = value?.Trim() ?? string.Empty;
        if (url.Length > BackgroundImageMaxLength) return string.Empty;
        if (url.Length == 0) return string.Empty;

        if (url.StartsWith('/')) return url;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        return string.Empty;
    }

    /// <summary>只接受站内内置模型或上传模型目录下的 .model3.json，其余回退为空</summary>
    internal static string NormalizeLive2DModelPath(string? value)
    {
        var path = value?.Trim() ?? string.Empty;
        if (path.Length is 0 or > 1024) return string.Empty;
        if (!path.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase)) return string.Empty;

        return path.StartsWith("/live2d/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/uploads/live2d/", StringComparison.OrdinalIgnoreCase)
            ? path
            : string.Empty;
    }

    /// <summary>空列表也会序列化为 JSON 数组</summary>
    internal static string SerializeLive2DOutfits(
        Live2DOutfitSelection[]? values)
    {
        return JsonSerializer.Serialize(
            NormalizeLive2DOutfits(values),
            SettingsJsonContext.Default.Live2DOutfitSelectionArray);
    }

    /// <summary>旧数据或损坏内容一律回退为空数组</summary>
    internal static Live2DOutfitSelection[] DeserializeLive2DOutfits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        try
        {
            var values = JsonSerializer.Deserialize(
                value, SettingsJsonContext.Default.Live2DOutfitSelectionArray);
            return NormalizeLive2DOutfits(values);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>去空白、按组去重，并限制条目数量与标识长度，避免异常数据落库</summary>
    internal static Live2DOutfitSelection[] NormalizeLive2DOutfits(
        Live2DOutfitSelection[]? values)
    {
        if (values is null) return [];

        var result = new List<Live2DOutfitSelection>();
        var seenGroups = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in values)
        {
            if (raw is null) continue;

            var groupId = raw.GroupId?.Trim() ?? string.Empty;
            var optionId = raw.OptionId?.Trim() ?? string.Empty;
            if (groupId.Length is 0 or > Live2DOutfitIdMaxLength) continue;
            if (optionId.Length is 0 or > Live2DOutfitIdMaxLength) continue;
            if (groupId.Any(char.IsControl) || optionId.Any(char.IsControl)) continue;
            if (!seenGroups.Add(groupId)) continue;

            result.Add(new Live2DOutfitSelection(groupId, optionId));
            if (result.Count >= Live2DOutfitMaxCount) break;
        }

        return result.ToArray();
    }

    /// <summary>空列表也会序列化为 JSON 数组</summary>
    internal static string SerializePublicHardware(string[]? values)
    {
        return JsonSerializer.Serialize(
            NormalizePublicHardware(values),
            SettingsJsonContext.Default.StringArray);
    }

    /// <summary>旧数据或损坏内容一律回退为空列表</summary>
    internal static string[] DeserializePublicHardware(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        try
        {
            var values = JsonSerializer.Deserialize(
                value, SettingsJsonContext.Default.StringArray);
            return NormalizePublicHardware(values);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string[] NormalizePublicHardware(string[]? values)
    {
        if (values is null) return [];

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in values)
        {
            if (raw is null) continue;

            var value = raw.Trim();
            if (value.Length is 0 or > PublicHardwareMaxLength) continue;
            if (value.Any(char.IsControl)) continue;
            if (!seen.Add(value)) continue;

            result.Add(value);
            if (result.Count >= PublicHardwareMaxCount) break;
        }

        return result.ToArray();
    }

    /// <summary>只做去空白与长度限制，路径格式由 Docker 端点解析决定</summary>
    internal static string NormalizeDockerSocket(string? value)
    {
        var socket = value?.Trim() ?? string.Empty;
        if (socket.Length > 512) return string.Empty;
        return socket.Any(char.IsControl) ? string.Empty : socket;
    }

    /// <summary>只接受 #rgb / #rrggbb，其余回退为空（沿用 Naive UI 默认主色）</summary>
    internal static string NormalizeColor(string? value)
    {
        var color = value?.Trim() ?? string.Empty;
        if (color.Length is not (4 or 7) || color[0] != '#') return string.Empty;

        foreach (var c in color.AsSpan(1))
            if (!char.IsAsciiHexDigit(c))
                return string.Empty;

        return color.ToLowerInvariant();
    }
}

internal static class SettingsMappingExtensions
{
    public static SettingsResponse ToResponse(this SettingsEntity entity)
    {
        var title = entity.SiteTitle?.Trim() ?? string.Empty;

        return new SettingsResponse
        {
            SiteTitle = title.Length == 0 ? "Atlas" : title,
            DesktopCols = entity.DesktopCols,
            DesktopGridContainerMaxWidth = entity.DesktopGridContainerMaxWidth,
            DesktopGridFullWidth = entity.DesktopGridFullWidth,
            BackgroundImage = entity.BackgroundImage ?? string.Empty,
            BackgroundBlur = entity.BackgroundBlur,
            BackgroundMask = entity.BackgroundMask,
            PrimaryColor = SettingsService.NormalizeColor(entity.PrimaryColor),
            PublicHardware =
                SettingsService.DeserializePublicHardware(entity.PublicHardware),
            SystemStatusEnabled = entity.SystemStatusEnabled,
            DockerEnabled = entity.DockerEnabled ?? true,
            DockerSocket = entity.DockerSocket ?? string.Empty,
            SiteSubtitle = entity.SiteSubtitle ?? string.Empty,
            Favicon = entity.Favicon ?? string.Empty,
            ShowHeaderLogo = entity.ShowHeaderLogo ?? true,
            FooterText = entity.FooterText ?? string.Empty,
            ServiceProbeEnabled = entity.ServiceProbeEnabled,
            Live2DEnabled = entity.Live2DEnabled,
            Live2DModelPath =
                SettingsService.NormalizeLive2DModelPath(entity.Live2DModelPath)
        };
    }
}