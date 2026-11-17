namespace Atlas.Features.Settings.Contracts;

public record UpdateSettingsRequest(
    string? SiteTitle,
    int DesktopCols,
    int DesktopGridContainerMaxWidth,
    bool DesktopGridFullWidth,
    string? BackgroundImage,
    int BackgroundBlur,
    int BackgroundMask,
    string? PrimaryColor,
    string[]? PublicHardware,
    bool SystemStatusEnabled,
    string? SiteSubtitle,
    string? Favicon,
    bool ShowHeaderLogo,
    string? FooterText,
    bool ServiceProbeEnabled,
    bool Live2DEnabled,
    string? Live2DModelPath);