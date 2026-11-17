using System.Text.Json.Nodes;
using Atlas.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Tests;

/// <summary>
///     端点错误出口必须产出 RFC 7807 ProblemDetails
///     用 DefaultHttpContext 直接执行 IResult，断言序列化后的真实响应体
/// </summary>
public class ProblemDetailsTests
{
    [Fact]
    public async Task 校验失败文案以400_problem_json的detail逐字返回()
    {
        const string message = "请选择备份文件";
        var (status, contentType, body) = await ExecuteAsync(
            ProblemResults.InvalidRequest(message));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal(message, (string?)body["detail"]);
        Assert.Equal(400, (int)body["status"]!);
        Assert.Equal("Bad Request", (string?)body["title"]);
        Assert.StartsWith("https://tools.ietf.org/html/rfc9110", (string?)body["type"]);
    }

    [Fact]
    public async Task 端点捕获InvalidRequestException后文案原样进入detail()
    {
        // 与端点约定一致的映射路径：catch (InvalidRequestException e) → ProblemResults.InvalidRequest(e.Message)
        ProblemHttpResult Map()
        {
            return ProblemResults.InvalidRequest(
                new InvalidRequestException("备份文件不是有效的 ZIP").Message);
        }

        var (status, _, body) = await ExecuteAsync(Map());

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("备份文件不是有效的 ZIP", (string?)body["detail"]);
    }

    [Fact]
    public async Task 无文案的错误状态码仍为problem_json且不含detail()
    {
        // 改造前本就返回空响应体的错误（如 404）保持无 detail，仅补齐 type/title/status
        var (status, contentType, body) = await ExecuteAsync(
            TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Null((string?)body["detail"]);
        Assert.Equal(404, (int)body["status"]!);
        Assert.Equal("Not Found", (string?)body["title"]);
    }

    private static async Task<(int Status, string? ContentType, JsonObject Body)> ExecuteAsync(
        IResult result)
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services() };
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var body = JsonNode.Parse(new StreamReader(httpContext.Response.Body).ReadToEnd())!
            .AsObject();

        return (httpContext.Response.StatusCode,
            httpContext.Response.ContentType,
            body!);
    }

    // 与 WebApplicationBuilder 默认一致地注册日志与 HttpJsonOptions，
    // 确保 ProblemHttpResult 序列化走与应用相同的选项
    private static IServiceProvider Services()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .Configure<JsonOptions>(_ => { })
            .BuildServiceProvider();
    }
}