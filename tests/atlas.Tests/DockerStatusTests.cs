using System.Text.Json;
using Atlas.Features.Docker;
using Atlas.Features.Docker.Contracts;

namespace Atlas.Tests;

public class DockerStatusTests
{
    private static readonly DateTimeOffset SampledAt =
        new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 聚合计数与容器字段按约定映射()
    {
        var snapshot = DockerSnapshot.FromApi(
        [
            new DockerApiContainer
            {
                Id = "aaaaaaaaaaaaaaaa",
                Names = ["/nginx"],
                Image = "nginx:alpine",
                State = "running",
                Status = "Up 3 hours"
            },
            new DockerApiContainer
            {
                Id = "bbbbbbbbbbbbbbbb",
                Names = ["/postgres"],
                State = "running"
            },
            new DockerApiContainer
            {
                Id = "cccccccccccccccc",
                Names = ["/cache"],
                State = "paused"
            },
            new DockerApiContainer
            {
                Id = "dddddddddddddddd",
                Names = ["/job"],
                State = "exited"
            }
        ], 7, [], SampledAt);

        Assert.True(snapshot.Available);
        Assert.Equal(4, snapshot.Total);
        Assert.Equal(2, snapshot.Running);
        Assert.Equal(1, snapshot.Paused);
        Assert.Equal(1, snapshot.Stopped);
        Assert.Equal(7, snapshot.Images);
        Assert.Equal("nginx", snapshot.Containers[0].Name);
        Assert.Equal("aaaaaaaaaaaa", snapshot.Containers[0].Id);
        Assert.Equal(SampledAt, snapshot.SampledAt);
    }

    [Fact]
    public void 解析DockerApi返回的字段()
    {
        const string json = """
                            [
                              {
                                "Id": "abcdef1234567890",
                                "Names": ["/nginx"],
                                "Image": "nginx:alpine",
                                "State": "running",
                                "Status": "Up 3 hours"
                              }
                            ]
                            """;

        var containers = JsonSerializer.Deserialize(
            json, DockerJsonContext.Default.DockerApiContainerArray);

        var container = Assert.Single(containers!);
        Assert.Equal("abcdef1234567890", container.Id);
        Assert.Equal("/nginx", Assert.Single(container.Names!));
        Assert.Equal("nginx:alpine", container.Image);
        Assert.Equal("running", container.State);
        Assert.Equal("Up 3 hours", container.Status);
    }

    [Fact]
    public void 解析DockerStats与Info字段()
    {
        const string statsJson = """
                                 {
                                   "cpu_stats": {
                                     "cpu_usage": { "total_usage": 200 },
                                     "system_cpu_usage": 2000,
                                     "online_cpus": 2
                                   },
                                   "precpu_stats": {
                                     "cpu_usage": { "total_usage": 100 },
                                     "system_cpu_usage": 1000
                                   },
                                   "memory_stats": {
                                     "usage": 300,
                                     "stats": { "total_inactive_file": 100 }
                                   }
                                 }
                                 """;

        var stats = JsonSerializer.Deserialize(
            statsJson, DockerJsonContext.Default.DockerApiStats);
        var info = JsonSerializer.Deserialize(
            """{"Images":12}""", DockerJsonContext.Default.DockerApiInfo);

        Assert.NotNull(stats);
        Assert.Equal(200, stats.CpuStats?.CpuUsage?.TotalUsage);
        Assert.Equal(2, stats.CpuStats?.OnlineCpus);
        Assert.Equal(300, stats.MemoryStats?.Usage);
        Assert.Equal(100, stats.MemoryStats?.Stats?.TotalInactiveFile);
        Assert.Equal(12, info?.Images);
    }

    [Fact]
    public void 没有运行中容器时资源用量为零()
    {
        var snapshot = DockerSnapshot.FromApi(
        [
            new DockerApiContainer { Id = "dddddddddddddddd", State = "exited" }
        ], 3, [], SampledAt);

        Assert.Equal(0, snapshot.Running);
        Assert.Equal(0d, snapshot.CpuPercent);
        Assert.Equal(0L, snapshot.MemoryBytes);
    }

    [Fact]
    public void 聚合容器CPU与内存()
    {
        var stats = new[]
        {
            new DockerApiStats
            {
                CpuStats = new DockerCpuStats
                {
                    CpuUsage = new DockerCpuUsage { TotalUsage = 200 },
                    SystemCpuUsage = 2000,
                    OnlineCpus = 2
                },
                PreCpuStats = new DockerCpuStats
                {
                    CpuUsage = new DockerCpuUsage { TotalUsage = 100 },
                    SystemCpuUsage = 1000
                },
                MemoryStats = new DockerMemoryStats
                {
                    Usage = 300,
                    Stats = new DockerMemoryDetail { TotalInactiveFile = 100 }
                }
            }
        };

        var (cpu, memory) = DockerStatsSummary.From(stats);

        Assert.NotNull(cpu);
        Assert.Equal(20d, cpu.Value);
        Assert.NotNull(memory);
        Assert.Equal(200L, memory.Value);
    }

    [Fact]
    public void 匿名响应只保留聚合数据()
    {
        var snapshot = DockerSnapshot.FromApi(
        [
            new DockerApiContainer { Id = "aaaaaaaaaaaaaaaa", State = "running" },
            new DockerApiContainer { Id = "bbbbbbbbbbbbbbbb", State = "exited" }
        ], 3, [], SampledAt);

        var response = snapshot.ToResponse(false);

        Assert.True(response.Enabled);
        Assert.True(response.Available);
        Assert.False(response.DetailsVisible);
        Assert.Equal(2, response.Total);
        Assert.Equal(1, response.Running);
        Assert.Equal(3, response.Images);
        Assert.Null(response.CpuPercent);
        Assert.Null(response.MemoryBytes);
        Assert.Empty(response.Containers);
    }

    [Fact]
    public void 登录响应返回完整容器列表()
    {
        var snapshot = DockerSnapshot.FromApi(
        [
            new DockerApiContainer
            {
                Id = "aaaaaaaaaaaaaaaa",
                Names = ["/nginx"],
                Image = "nginx:alpine",
                State = "running",
                Status = "Up 3 hours (healthy)"
            }
        ], 5, [], SampledAt);

        var response = snapshot.ToResponse(true);

        Assert.True(response.DetailsVisible);
        var container = Assert.Single(response.Containers);
        Assert.Equal("nginx", container.Name);
        Assert.Equal("nginx:alpine", container.Image);
        Assert.Equal("Up 3 hours (healthy)", container.Status);
    }

    [Fact]
    public void 不可用与禁用响应()
    {
        var unavailable = DockerSnapshot.Unavailable(SampledAt).ToResponse(true);
        Assert.True(unavailable.Enabled);
        Assert.False(unavailable.Available);
        Assert.Equal(0, unavailable.Total);
        Assert.Empty(unavailable.Containers);

        var disabled = DockerStatusResponse.Disabled();
        Assert.False(disabled.Enabled);
        Assert.False(disabled.Available);
        Assert.Empty(disabled.Containers);
    }

    [Theory]
    [InlineData("unix:///var/run/docker.sock", "/var/run/docker.sock")]
    [InlineData("/var/run/docker.sock", "/var/run/docker.sock")]
    public void 解析UnixSocket路径(string endpoint, string expected)
    {
        var connection = DockerStatusService.ParseConnection(endpoint);

        Assert.NotNull(connection);
        Assert.Equal(expected, connection.UnixSocketPath);
        Assert.Null(connection.PipeName);
    }

    [Theory]
    [InlineData("npipe:////./pipe/docker_engine")]
    [InlineData(@"//./pipe/docker_engine")]
    [InlineData(@"\\.\pipe\docker_engine")]
    public void 解析Windows命名管道(string endpoint)
    {
        var connection = DockerStatusService.ParseConnection(endpoint);

        Assert.NotNull(connection);
        Assert.Equal("docker_engine", connection.PipeName);
        Assert.Null(connection.UnixSocketPath);
    }

    [Fact]
    public void 解析TCP端点()
    {
        var connection = DockerStatusService.ParseConnection("tcp://127.0.0.1:2375");

        Assert.NotNull(connection);
        Assert.Equal("http", connection.BaseAddress.Scheme);
        Assert.Equal("127.0.0.1", connection.BaseAddress.Host);
        Assert.Equal(2375, connection.BaseAddress.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://127.0.0.1:21")]
    [InlineData("unix://")]
    public void 无效端点返回空(string endpoint)
    {
        Assert.Null(DockerStatusService.ParseConnection(endpoint));
    }
}
