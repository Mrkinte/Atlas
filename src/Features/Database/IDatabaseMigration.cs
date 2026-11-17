namespace Atlas.Features.Database;

/// <summary>
///     一次性数据库结构迁移，每个版本只执行一次，执行记录保存在 Configs 表
/// </summary>
public interface IDatabaseMigration
{
    /// <summary>全局唯一，按升序执行</summary>
    int Version { get; }

    string Description { get; }

    /// <summary>由执行器包裹在事务中，抛出异常时整体回滚</summary>
    void Up(IFreeSql fsql);
}