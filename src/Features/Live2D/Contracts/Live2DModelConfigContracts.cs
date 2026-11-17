using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Live2D.Contracts;

/// <summary>单个看板娘模型的完整行为配置</summary>
public record Live2DModelConfigResponse(
    string ModelPath,
    string EntryMotionId,
    string IdleMotionId,
    string Expression,
    Live2DOutfitSelection[] Outfits,
    bool SoundEnabled,
    double Scale,
    double PositionX,
    double PositionY,
    double? FreeRatioX,
    double? FreeRatioY);

/// <summary>保存模型配置的请求，前端提交完整对象</summary>
public record UpdateLive2DModelConfigRequest(
    string? ModelPath,
    string? EntryMotionId,
    string? IdleMotionId,
    string? Expression,
    Live2DOutfitSelection[]? Outfits,
    bool? SoundEnabled,
    double? Scale,
    double? PositionX,
    double? PositionY,
    double? FreeRatioX,
    double? FreeRatioY);