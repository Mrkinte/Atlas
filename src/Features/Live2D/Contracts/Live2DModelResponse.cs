namespace Atlas.Features.Live2D.Contracts;

/// <summary>
///     上传目录中的一个 Live2D 模型。Path 指向 .model3.json，Id 同时用作删除标识
/// </summary>
public record Live2DModelResponse(
    string Id,
    string Name,
    string Path,
    long SizeBytes);