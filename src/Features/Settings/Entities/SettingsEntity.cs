using FreeSql.DataAnnotations;

namespace Atlas.Features.Settings.Entities;

[Table(Name = "Settings")]
public class SettingsEntity
{
    /// <summary>单行配置的固定主键</summary>
    [Column(IsPrimary = true, IsIdentity = false)]
    public int Id { get; set; }

    [Column(StringLength = 64)] public string? SiteTitle { get; set; }

    public int DesktopCols { get; set; }

    public int DesktopGridContainerMaxWidth { get; set; }

    /// <summary>忽略宽度上限，让卡片区域铺满屏幕</summary>
    public bool DesktopGridFullWidth { get; set; }

    /// <summary>空字符串表示不显示</summary>
    [Column(StringLength = 1024)]
    public string? BackgroundImage { get; set; }

    /// <summary>模糊半径（px）</summary>
    public int BackgroundBlur { get; set; }

    /// <summary>遮罩强度（%）</summary>
    public int BackgroundMask { get; set; }

    /// <summary>#rrggbb；空值表示沿用 Naive UI 默认主色</summary>
    [Column(StringLength = 9)]
    public string? PrimaryColor { get; set; }

    /// <summary>匿名可查看的硬件 ID 列表，以 JSON 数组存储</summary>
    [Column(StringLength = 4096)]
    public string? PublicHardware { get; set; }

    public bool SystemStatusEnabled { get; set; }

    /// <summary>打开首页时是否探测服务卡片的可达性</summary>
    public bool ServiceProbeEnabled { get; set; } = true;

    /// <summary>显示在登录页标题下方</summary>
    [Column(StringLength = 200)]
    public string? SiteSubtitle { get; set; }

    /// <summary>空字符串表示使用内置图标</summary>
    [Column(StringLength = 1024)]
    public string? Favicon { get; set; }

    /// <summary>页头标题左侧是否显示站点图标；NULL 表示引入该配置前的存量行，按显示处理</summary>
    public bool? ShowHeaderLogo { get; set; }

    /// <summary>空字符串表示不显示</summary>
    [Column(StringLength = 200)]
    public string? FooterText { get; set; }

    public bool Live2DEnabled { get; set; }

    /// <summary>空字符串表示使用内置模型</summary>
    [Column(StringLength = 1024)]
    public string? Live2DModelPath { get; set; }
}