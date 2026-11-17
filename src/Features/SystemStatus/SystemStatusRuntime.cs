namespace Atlas.Features.SystemStatus;

/// <summary>
///     系统状态开关的运行期状态。设置保存后立即更新，已建立的 WebSocket 连接
///     在下一轮推送时读取该值并主动断开
/// </summary>
public sealed class SystemStatusRuntime
{
    private volatile bool _enabled = true;

    public bool Enabled => _enabled;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }
}