using System.Reflection;
using System.Xml.Linq;
using Atlas.Tests.TestSupport;
using FreeSql.DataAnnotations;

namespace Atlas.Tests;

/// <summary>
///     实体登记防遗漏：EnsureSchema 登记的实体、TrimmerRoots.xml 的 AOT 根、
///     Features/*/Entities 下实际存在的实体三者必须一致
///     FreeSql 依赖反射读取实体元数据，未登记的实体会被 AOT 裁掉，发布版运行时才崩溃
/// </summary>
public class EntityRegistrationTests
{
    /// <summary>EnsureSchema 实际登记的实体全名集合</summary>
    private static string[] RegisteredEntityNames()
    {
        return AppInitializer.SchemaEntityTypes.Select(t => t.FullName!).Order().ToArray();
    }

    /// <summary>TrimmerRoots.xml 中登记的实体全名集合</summary>
    private static string[] TrimmerRootEntityNames()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TrimmerRoots.xml"));
        return document.Descendants("type")
            .Select(element => (string?)element.Attribute("fullname"))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Order()
            .ToArray();
    }

    /// <summary>
    ///     扫描程序集中实际存在的 FreeSql 实体：Features 各模块 Entities 命名空间下的类型 + 组合根私有 ConfigEntity
    ///     若未来新增实体放在别处，请同步调整扫描口径，否则这里等于形同虚设
    /// </summary>
    private static string[] ScannedEntityNames()
    {
        var assembly = typeof(AppInitializer).Assembly;
        return assembly.GetTypes()
            .Where(type => type is { IsClass: true }
                           && ((type.Namespace == "Atlas" && type.Name == "ConfigEntity")
                               || (type.Namespace?.EndsWith(".Entities") == true
                                   && type.Name.EndsWith("Entity"))))
            .Select(type => type.FullName!)
            .Order()
            .ToArray();
    }

    [Fact]
    public void EnsureSchema登记的实体与TrimmerRoots与Features实体三方一致()
    {
        var registered = RegisteredEntityNames();
        var roots = TrimmerRootEntityNames();
        var scanned = ScannedEntityNames();

        Assert.NotEmpty(registered);
        // 1) EnsureSchema ↔ TrimmerRoots：新实体忘了写 AOT 根，发布版会直接崩
        Assert.Equal(registered, roots);
        // 2) EnsureSchema ↔ Features 实体扫描：新实体忘了进 EnsureSchema，建表会漏
        Assert.Equal(registered, scanned);
    }

    [Fact]
    public void EnsureSchema执行后每个登记实体对应的表都真实存在()
    {
        // 行为面兜底：清单即使与调用漂移，表不存在也当场暴露
        using var host = new TestHost();
        host.InitializeSchema();

        var tables = host.QueryFirstColumn("SELECT name FROM sqlite_master WHERE type = 'table'");
        foreach (var type in AppInitializer.SchemaEntityTypes)
        {
            // 与 FreeSql 的命名规则一致：显式 [Table(Name)] 优先，否则用类名
            var tableName = type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;
            Assert.Contains(tableName, tables, StringComparer.OrdinalIgnoreCase);
        }
    }
}