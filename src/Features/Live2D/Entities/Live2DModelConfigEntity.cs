using FreeSql.DataAnnotations;

namespace Atlas.Features.Live2D.Entities;

/// <summary>
///     按模型保存的看板娘配置：开机/待机动作、表情、服装、语音开关与舞台布局
///     以模型清单地址为主键，内置模型与上传模型共用同一份结构
/// </summary>
[Table(Name = "Live2DModelConfigs")]
public class Live2DModelConfigEntity
{
    /// <summary>模型清单地址，例如 /uploads/live2d/{id}/xxx.model3.json</summary>
    [Column(IsPrimary = true, IsIdentity = false, StringLength = 1024)]
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>清单动作 id 或动作文件路径，空字符串表示使用清单默认</summary>
    [Column(StringLength = 256)]
    public string? EntryMotionId { get; set; }

    /// <summary>清单动作 id 或动作文件路径，空字符串表示不设置</summary>
    [Column(StringLength = 256)]
    public string? IdleMotionId { get; set; }

    /// <summary>默认表情的运行名，空字符串表示使用清单默认</summary>
    [Column(StringLength = 128)]
    public string? Expression { get; set; }

    /// <summary>各服装组当前选中的选项，以 JSON 数组存储</summary>
    [Column(StringLength = 8192)]
    public string? Outfits { get; set; } = "[]";

    public bool SoundEnabled { get; set; } = true;

    public double Scale { get; set; }

    /// <summary>按可移动范围的 0~1 比例保存</summary>
    public double PositionX { get; set; }

    /// <summary>按可移动范围的 0~1 比例保存</summary>
    public double PositionY { get; set; }

    /// <summary>关闭边缘吸附时的水平位置，按视口宽度比例保存，可为负值或大于 1；null 表示尚未记录</summary>
    public double? FreeRatioX { get; set; }

    /// <summary>关闭边缘吸附时的垂直位置，按视口高度比例保存，可为负值或大于 1；null 表示尚未记录</summary>
    public double? FreeRatioY { get; set; }

    public DateTime UpdatedAt { get; set; }
}