namespace Atlas.Features.About.Contracts;

/// <summary>
///     进程内存与 GC 运行配置快照，用于区分"托管堆里真有对象"与"GC 预留/提交导致 RSS 偏高"，
///     不抓 dump 也能判断内存优化是否生效
/// </summary>
public record AboutMemoryInfo(
    long WorkingSetBytes,
    long ManagedHeapBytes,
    long CommittedBytes,
    long FragmentedBytes,
    bool IsServerGc,
    bool IsConcurrentGc,
    int ConserveMemory,
    int HeapCount);