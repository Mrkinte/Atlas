using System.Net;
using System.Text;
using Atlas.Features.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task 解析最新Release并标记可更新()
    {
        const string json = """
                            {
                              "tag_name": "V9.8.7",
                              "body": "## 更新\n- 修复问题",
                              "prerelease": false,
                              "published_at": "2026-11-16T08:00:00+08:00"
                            }
                            """;

        var service = CreateService(json);

        var result = await service.CheckAsync(false, CancellationToken.None);

        Assert.Equal("9.8.7", result.LatestVersion);
        Assert.True(result.UpdateAvailable);
        Assert.False(result.IsPrerelease);
        Assert.Equal("## 更新\n- 修复问题", result.ReleaseNotes);
        Assert.Equal("https://github.com/acme/atlas/releases/tag/V9.8.7", result.ReleaseUrl);
        Assert.Equal(
            new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero), result.PublishedAt);
    }

    [Fact]
    public async Task 预发布Release带上标记()
    {
        const string json = """
                            {
                              "tag_name": "v1.1.0-beta",
                              "body": null,
                              "prerelease": true,
                              "published_at": "2026-11-16T08:00:00+08:00"
                            }
                            """;

        var service = CreateService(json);

        var result = await service.CheckAsync(false, CancellationToken.None);

        Assert.True(result.IsPrerelease);
        Assert.Null(result.ReleaseNotes);
    }

    [Fact]
    public async Task 没有Release时返回可展示的错误()
    {
        var service = CreateService(
            """{"message":"Not Found"}""",
            HttpStatusCode.NotFound);

        var error = await Assert.ThrowsAsync<UpdateUnavailableException>(
            () => service.CheckAsync(false, CancellationToken.None));

        Assert.Equal("未找到 GitHub 仓库或尚未发布 Release", error.Message);
    }

    [Fact]
    public async Task 命中缓存后不再重复请求()
    {
        var handler = new CountingHandler(
            """{"tag_name":"V1.0.0","body":"首版","prerelease":false}""");

        var service = new UpdateService(
            new StubHttpClientFactory(handler), CreateConfiguration(),
            NullLogger<UpdateService>.Instance);

        await service.CheckAsync(false, CancellationToken.None);
        await service.CheckAsync(false, CancellationToken.None);

        Assert.Equal(1, handler.Count);
    }

    private static UpdateService CreateService(
        string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new UpdateService(
            new StubHttpClientFactory(new CountingHandler(json, status)),
            CreateConfiguration(),
            NullLogger<UpdateService>.Instance);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Atlas:Update:Repository"] = "acme/atlas" }).Build();
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, false);
        }
    }

    private sealed class CountingHandler(string json, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;

            Assert.Equal(
                "https://api.github.com/repos/acme/atlas/releases/latest",
                request.RequestUri?.ToString());
            Assert.Equal("atlas-update-check", request.Headers.UserAgent.ToString());

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
