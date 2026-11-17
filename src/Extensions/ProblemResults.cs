using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Extensions;

/// <summary>
///     请求校验失败或业务规则不满足时抛出，端点捕获后经 <see cref="ProblemResults" /> 转为 400
/// </summary>
public sealed class InvalidRequestException(string message) : Exception(message);

/// <summary>
///     端点错误响应的统一出口，均以 RFC 7807 ProblemDetails 返回，
///     面向用户的文案放在 detail，由前端直接展示
/// </summary>
public static class ProblemResults
{
    /// <summary>400，detail 直接展示给用户</summary>
    public static ProblemHttpResult InvalidRequest(string detail)
    {
        return TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
    }
}