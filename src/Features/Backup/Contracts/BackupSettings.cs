using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Backup.Contracts;

/// <summary>
///     备份中的站点配置，与接口响应解耦，接口字段调整不会改变备份格式
///     字段只允许新增，删除或改语义时必须提升备份格式版本并提供迁移
/// </summary>
public class BackupSettings
{
    public string SiteTitle { get; set; } = string.Empty;

    public int DesktopCols { get; set; }

    public int DesktopGridContainerMaxWidth { get; set; }

    public bool DesktopGridFullWidth { get; set; }

    public string BackgroundImage { get; set; } = string.Empty;

    public int BackgroundBlur { get; set; }

    public int BackgroundMask { get; set; }

    public string PrimaryColor { get; set; } = string.Empty;

    public string[] PublicHardware { get; set; } = [];

    public bool SystemStatusEnabled { get; set; }

    /// <summary>旧备份没有该字段时按启用处理</summary>
    public bool? DockerEnabled { get; set; }

    public string DockerSocket { get; set; } = string.Empty;

    public string SiteSubtitle { get; set; } = string.Empty;

    public string Favicon { get; set; } = string.Empty;

    public bool ShowHeaderLogo { get; set; } = true;

    public string FooterText { get; set; } = string.Empty;

    public bool ServiceProbeEnabled { get; set; }

    internal static BackupSettings FromResponse(SettingsResponse source)
    {
        return new BackupSettings
        {
            SiteTitle = source.SiteTitle,
            DesktopCols = source.DesktopCols,
            DesktopGridContainerMaxWidth =
                source.DesktopGridContainerMaxWidth,
            DesktopGridFullWidth = source.DesktopGridFullWidth,
            BackgroundImage = source.BackgroundImage,
            BackgroundBlur = source.BackgroundBlur,
            BackgroundMask = source.BackgroundMask,
            PrimaryColor = source.PrimaryColor,
            PublicHardware = [.. source.PublicHardware],
            SystemStatusEnabled = source.SystemStatusEnabled,
            DockerEnabled = source.DockerEnabled,
            DockerSocket = source.DockerSocket,
            SiteSubtitle = source.SiteSubtitle,
            Favicon = source.Favicon,
            ShowHeaderLogo = source.ShowHeaderLogo,
            FooterText = source.FooterText,
            ServiceProbeEnabled = source.ServiceProbeEnabled
        };
    }
}