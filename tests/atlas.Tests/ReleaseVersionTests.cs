using Atlas.Features.Update;

namespace Atlas.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3", 0)]
    [InlineData("1.2.3-dev", "v1.2.3", 0)]
    [InlineData("1.2.3+build.7", "1.2.3", 0)]
    [InlineData("v1.10.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("2.0", "1.9.9", 1)]
    public void 比较忽略前缀与后缀(string left, string right, int expected)
    {
        Assert.True(ReleaseVersion.TryParse(left, out var first));
        Assert.True(ReleaseVersion.TryParse(right, out var second));

        Assert.Equal(expected, Math.Sign(first.CompareTo(second)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("v")]
    [InlineData("latest")]
    [InlineData("1.x.0")]
    [InlineData("1.2.3.4.5")]
    public void 无法识别的版本返回失败(string value)
    {
        Assert.False(ReleaseVersion.TryParse(value, out _));
    }
}
