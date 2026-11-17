using System.Reflection;

namespace Atlas.Extensions;

/// <summary>程序集版本号，去掉 +hash 构建元数据后与发布标签一致</summary>
internal static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(AppVersion).Assembly;

        var value = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? assembly.GetName().Version?.ToString()
                    ?? "0.0.0";

        var index = value.IndexOf('+');
        return index > 0 ? value[..index] : value;
    }
}
