namespace Atlas.Features.Settings.Contracts;

/// <summary>
///     站点配置，全部字段一次性下发
/// </summary>
public class SettingsResponse
{
    public string SiteTitle { get; init; } = string.Empty;

    public int DesktopCols { get; init; }

    public int DesktopGridContainerMaxWidth { get; init; }

    public bool DesktopGridFullWidth { get; init; }

    public string BackgroundImage { get; init; } = string.Empty;

    public int BackgroundBlur { get; init; }

    public int BackgroundMask { get; init; }

    public string PrimaryColor { get; init; } = string.Empty;

    /// <summary>匿名响应会清空该字段，因此允许返回前改写</summary>
    public string[] PublicHardware { get; set; } = [];

    public bool SystemStatusEnabled { get; init; }

    public bool DockerEnabled { get; init; } = true;

    /// <summary>匿名响应会清空该字段，因此允许返回前改写</summary>
    public string DockerSocket { get; set; } = string.Empty;

    public string SiteSubtitle { get; init; } = string.Empty;

    public string Favicon { get; init; } = string.Empty;

    public bool ShowHeaderLogo { get; init; } = true;

    public string FooterText { get; init; } = string.Empty;

    public bool ServiceProbeEnabled { get; init; }

    public bool Live2DEnabled { get; init; }

    /// <summary>空字符串表示使用内置模型</summary>
    public string Live2DModelPath { get; init; } = string.Empty;
}