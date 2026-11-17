using Atlas.Features.Live2D.Contracts;
using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Backup.Contracts;

/// <summary>Live2D 段的启用状态、当前模型与各模型配置</summary>
public class BackupLive2D
{
    public bool Enabled { get; set; }

    /// <summary>空字符串表示使用内置模型</summary>
    public string ModelPath { get; set; } = string.Empty;

    public List<BackupLive2DConfig> Configs { get; set; } = [];

    internal static BackupLive2D FromResponse(
        SettingsResponse settings, IEnumerable<Live2DModelConfigResponse> configs)
    {
        return new BackupLive2D
        {
            Enabled = settings.Live2DEnabled,
            ModelPath = settings.Live2DModelPath,
            Configs = configs.Select(BackupLive2DConfig.FromResponse).ToList()
        };
    }
}