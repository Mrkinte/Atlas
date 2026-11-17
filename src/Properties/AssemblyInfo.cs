using System.Runtime.CompilerServices;

// 回归测试需要读取 internal 成员（AppInitializer.SchemaEntityTypes 实体登记清单），
// 用于断言 EnsureSchema 登记、TrimmerRoots.xml 与 Features 实体三方一致
[assembly: InternalsVisibleTo("atlas.Tests")]