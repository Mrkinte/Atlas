namespace Atlas.Features.Update;

/// <summary>检查更新失败，Message 面向用户，由端点转成 502 ProblemDetails</summary>
public sealed class UpdateUnavailableException(string message) : Exception(message);
