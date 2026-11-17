using Atlas.Features.Live2D.Contracts;
using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Backup.Contracts;

/// <summary>
///     备份中单个看板娘模型的配置，包含行为选择与舞台布局
///     布局字段从格式 v2 开始随模型保存
/// </summary>
public class BackupLive2DConfig
{
    public string ModelPath { get; set; } = string.Empty;

    public string EntryMotionId { get; set; } = string.Empty;

    public string IdleMotionId { get; set; } = string.Empty;

    public string Expression { get; set; } = string.Empty;

    public Live2DOutfitSelection[] Outfits { get; set; } = [];

    public bool SoundEnabled { get; set; }

    public double Scale { get; set; }

    public double PositionX { get; set; }

    public double PositionY { get; set; }

    public double? FreeRatioX { get; set; }

    public double? FreeRatioY { get; set; }

    internal static BackupLive2DConfig FromResponse(
        Live2DModelConfigResponse source)
    {
        return new BackupLive2DConfig
        {
            ModelPath = source.ModelPath,
            EntryMotionId = source.EntryMotionId,
            IdleMotionId = source.IdleMotionId,
            Expression = source.Expression,
            Outfits = source.Outfits,
            SoundEnabled = source.SoundEnabled,
            Scale = source.Scale,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            FreeRatioX = source.FreeRatioX,
            FreeRatioY = source.FreeRatioY
        };
    }
}