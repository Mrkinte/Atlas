using Atlas.Extensions;
using Atlas.Features.Live2D.Contracts;
using Atlas.Features.Live2D.Entities;
using Atlas.Features.Settings;

namespace Atlas.Features.Live2D;

/// <summary>
///     按模型读写看板娘配置。只保存标识、选择结果与舞台布局，动作文件仍从模型清单读取
/// </summary>
public class Live2DConfigService(IFreeSql fsql)
{
    private const int MotionIdMaxLength = 256;

    private const int ExpressionMaxLength = 128;

    private const double DefaultScale = 0.12;

    private const double ScaleMin = 0.05;

    private const double ScaleMax = 0.5;

    // 位置按可移动范围的 0~1 比例保存
    private const double PositionMin = 0;

    private const double PositionMax = 1;

    // 自由摆放位置比例的绝对值上限，避免异常数据落库
    private const double FreeRatioLimit = 100;

    public async Task<Live2DModelConfigResponse> GetAsync(
        string? modelPath, CancellationToken ct = default)
    {
        var path = NormalizeModelPath(modelPath);
        var entity = await fsql.Select<Live2DModelConfigEntity>()
            .Where(item => item.ModelPath == path)
            .FirstAsync(ct);

        return entity is null ? Empty(path) : entity.ToResponse();
    }

    public async Task<Live2DModelConfigResponse> SaveAsync(
        UpdateLive2DModelConfigRequest request, CancellationToken ct = default)
    {
        var path = NormalizeModelPath(request.ModelPath);
        var layout = NormalizeLayout(
            request.Scale,
            request.PositionX,
            request.PositionY,
            request.FreeRatioX,
            request.FreeRatioY);
        var entity = new Live2DModelConfigEntity
        {
            ModelPath = path,
            EntryMotionId = NormalizeMotionId(request.EntryMotionId),
            IdleMotionId = NormalizeMotionId(request.IdleMotionId),
            Expression = NormalizeExpression(request.Expression),
            Outfits = SettingsService.SerializeLive2DOutfits(request.Outfits),
            // 请求缺省时保持语音开启，避免旧客户端保存配置时意外静音
            SoundEnabled = request.SoundEnabled ?? true,
            Scale = layout.Scale,
            PositionX = layout.PositionX,
            PositionY = layout.PositionY,
            FreeRatioX = layout.FreeRatioX,
            FreeRatioY = layout.FreeRatioY,
            UpdatedAt = DateTime.UtcNow
        };

        await fsql.InsertOrUpdate<Live2DModelConfigEntity>()
            .SetSource(entity)
            .ExecuteAffrowsAsync(ct);

        return entity.ToResponse();
    }

    public int DeleteByModelPrefix(string prefix)
    {
        return fsql.Delete<Live2DModelConfigEntity>()
            .Where(item => item.ModelPath.StartsWith(prefix))
            .ExecuteAffrows();
    }

    private static Live2DModelConfigResponse Empty(string modelPath)
    {
        return new Live2DModelConfigResponse(
            modelPath,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            true,
            DefaultScale,
            0,
            1,
            null,
            null);
    }

    /// <summary>
    ///     缩放缺失或非正数视为旧数据，统一回退默认布局，
    ///     避免新增列后模型位置跑到左上角
    /// </summary>
    internal static Live2DLayout NormalizeLayout(
        double? scale,
        double? positionX,
        double? positionY,
        double? freeRatioX,
        double? freeRatioY)
    {
        if (scale is null || !double.IsFinite(scale.Value) || scale.Value <= 0)
            return new Live2DLayout(DefaultScale, 0, 1, null, null);

        return new Live2DLayout(
            Math.Clamp(scale.Value, ScaleMin, ScaleMax),
            NormalizePosition(positionX, 0),
            NormalizePosition(positionY, 1),
            NormalizeFreeRatio(freeRatioX),
            NormalizeFreeRatio(freeRatioY));
    }

    private static double NormalizePosition(double? value, double fallback)
    {
        if (value is null || !double.IsFinite(value.Value)) return fallback;
        return Math.Clamp(value.Value, PositionMin, PositionMax);
    }

    /// <summary>自由摆放以视口宽高为基准，允许负值与超出 1，只拦非有限值并限制绝对值</summary>
    private static double? NormalizeFreeRatio(double? value)
    {
        if (value is null || !double.IsFinite(value.Value)) return null;
        return Math.Clamp(value.Value, -FreeRatioLimit, FreeRatioLimit);
    }

    private static string NormalizeModelPath(string? value)
    {
        var path = SettingsService.NormalizeLive2DModelPath(value);
        if (path.Length == 0) throw new InvalidRequestException("模型地址不合法");
        return path;
    }

    private static string NormalizeMotionId(string? value)
    {
        return NormalizeText(value, MotionIdMaxLength);
    }

    private static string NormalizeExpression(string? value)
    {
        return NormalizeText(value, ExpressionMaxLength);
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length > maxLength) text = text[..maxLength];
        return text.Any(char.IsControl) ? string.Empty : text;
    }

    internal readonly record struct Live2DLayout(
        double Scale,
        double PositionX,
        double PositionY,
        double? FreeRatioX,
        double? FreeRatioY);
}

internal static class Live2DModelConfigMappingExtensions
{
    public static Live2DModelConfigResponse ToResponse(
        this Live2DModelConfigEntity entity)
    {
        var layout = Live2DConfigService.NormalizeLayout(
            entity.Scale,
            entity.PositionX,
            entity.PositionY,
            entity.FreeRatioX,
            entity.FreeRatioY);

        return new Live2DModelConfigResponse(
            entity.ModelPath,
            entity.EntryMotionId ?? string.Empty,
            entity.IdleMotionId ?? string.Empty,
            entity.Expression ?? string.Empty,
            SettingsService.DeserializeLive2DOutfits(entity.Outfits),
            entity.SoundEnabled,
            layout.Scale,
            layout.PositionX,
            layout.PositionY,
            layout.FreeRatioX,
            layout.FreeRatioY);
    }
}