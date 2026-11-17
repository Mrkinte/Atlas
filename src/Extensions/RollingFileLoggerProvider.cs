using System.Globalization;
using System.Text;
using System.Threading.Channels;

namespace atlas.Extensions;

/// <summary>
///     按天切割的文件日志，写入数据目录下的 logs。写盘在后台任务中完成，
///     业务线程只负责入队；启动与跨天切割时清理超过保留天数的旧文件
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly string _filePrefix;

    private readonly Channel<LogEntry> _queue = Channel.CreateUnbounded<LogEntry>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly int _retentionDays;

    private readonly Task _writerTask;
    private volatile bool _disposed;

    public RollingFileLoggerProvider(string directory, int retentionDays = 7, string filePrefix = "atlas")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        _directory = directory;
        _filePrefix = filePrefix;
        _retentionDays = retentionDays;

        // 目录创建失败不阻断启动：写入失败会退到控制台，而不是让应用起不来
        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[{Now()}] 日志目录创建失败：{_directory}，文件日志不可用。{ex.Message}");
        }

        _writerTask = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RollingFileLogger(this, categoryName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _queue.Writer.TryComplete();

        // 停机时给后台任务一点时间把队列里的日志落盘
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
        }
    }

    private static string Now()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void Enqueue(LogEntry entry)
    {
        if (_disposed) return;
        _queue.Writer.TryWrite(entry);
    }

    private async Task WriteLoopAsync()
    {
        StreamWriter? writer = null;
        DateOnly? currentDate = null;

        // 进程可能跨天甚至跨周重启，等下次切割才清理会晚很久
        Cleanup(DateOnly.FromDateTime(DateTime.Now));

        try
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var entry))
                    try
                    {
                        var date = DateOnly.FromDateTime(entry.Timestamp);
                        if (writer is null || currentDate != date)
                        {
                            writer?.Dispose();
                            writer = OpenWriter(date);
                            currentDate = date;
                            Cleanup(date);
                        }

                        writer.WriteLine(Format(entry));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // 日志系统自身出错不能再抛给业务，否则正常流程会被磁盘问题拖垮
                        Console.Error.WriteLine($"[{Now()}] 文件日志写入失败：{ex.Message}");
                        writer?.Dispose();
                        writer = null;
                        currentDate = null;
                    }

                writer?.Flush();
            }
        }
        finally
        {
            writer?.Dispose();
        }
    }

    private StreamWriter OpenWriter(DateOnly date)
    {
        var path = Path.Combine(_directory, $"{_filePrefix}-{date:yyyy-MM-dd}.log");

        // FileShare.Read 允许运行中直接 tail，但不允许第二个写入者并发追加
        return new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false));
    }

    /// <summary>删除文件名日期早于「今天 - (保留天数 - 1)」的日志，只处理本 Provider 的命名</summary>
    private void Cleanup(DateOnly today)
    {
        var oldestKept = today.AddDays(-(_retentionDays - 1));
        var prefix = _filePrefix + "-";

        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, prefix + "*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!DateOnly.TryParseExact(name[prefix.Length..], "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
                if (date >= oldestKept) continue;

                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"[{Now()}] 旧日志删除失败：{file}，{ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[{Now()}] 日志目录清理失败：{ex.Message}");
        }
    }

    private static string Format(LogEntry entry)
    {
        var builder = new StringBuilder(256);
        builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(LevelName(entry.Level)).Append("] ")
            .Append(entry.Category).Append(" - ").Append(entry.Message);

        if (entry.Exception is not null) builder.AppendLine().Append(entry.Exception);

        return builder.ToString();
    }

    private static string LevelName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => "NONE"
        };
    }

    private sealed record LogEntry(
        DateTime Timestamp,
        LogLevel Level,
        string Category,
        string Message,
        Exception? Exception);

    /// <summary>不记录日志作用域，返回空作用域以满足 ILogger 契约</summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RollingFileLogger(
        RollingFileLoggerProvider provider,
        string category) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && !provider._disposed;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            provider.Enqueue(new LogEntry(
                DateTime.Now, logLevel, category, formatter(state, exception), exception));
        }
    }
}