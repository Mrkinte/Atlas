using Atlas.Extensions;
using Microsoft.Extensions.Configuration;

namespace Atlas.Tests;

public class AtlasPathsTests
{
    [Fact]
    public void 命令行覆盖优先级最高()
    {
        var configuration = Build(
            ("Atlas:DataDirectory", "/config"),
            ("ATLAS_DATA_DIR", "/env"));

        var paths = AtlasPaths.Resolve(configuration, "/content", "/cli");

        Assert.Equal("/cli", paths.DataDirectory);
        Assert.Equal(DataDirectorySource.CommandLine, paths.Source);
    }

    [Fact]
    public void 环境变量高于配置项()
    {
        var configuration = Build(
            ("Atlas:DataDirectory", "/config"),
            ("ATLAS_DATA_DIR", "/env"));

        var paths = AtlasPaths.Resolve(configuration, "/content");

        Assert.Equal("/env", paths.DataDirectory);
        Assert.Equal(DataDirectorySource.Environment, paths.Source);
    }

    [Fact]
    public void 配置项高于默认值()
    {
        var configuration = Build(("Atlas:DataDirectory", "/config"));

        var paths = AtlasPaths.Resolve(configuration, "/content");

        Assert.Equal("/config", paths.DataDirectory);
        Assert.Equal(DataDirectorySource.Configuration, paths.Source);
    }

    [Fact]
    public void 未配置时使用内容根()
    {
        var paths = AtlasPaths.Resolve(Build(), "/content");

        Assert.Equal("/content", paths.DataDirectory);
        Assert.Equal(DataDirectorySource.Default, paths.Source);
    }

    [Fact]
    public void 相对路径基于内容根绝对化()
    {
        var configuration = Build(("Atlas:DataDirectory", "data"));

        var paths = AtlasPaths.Resolve(configuration, "/srv/atlas");

        Assert.Equal("/srv/atlas/data", paths.DataDirectory);
    }

    private static IConfiguration Build(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();
    }
}
