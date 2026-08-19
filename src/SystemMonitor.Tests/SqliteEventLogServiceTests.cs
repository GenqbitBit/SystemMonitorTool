using System;
using System.Linq;
using System.Threading.Tasks;
using SystemMonitor.Infrastructure.Persistence;
using Xunit;

namespace SystemMonitor.Tests;

public class SqliteEventLogServiceTests
{
    [Fact]
    public async Task ConcurrentReadAndWriteOperations_DoNotThrow()
    {
        using var service = new SqliteEventLogService();

        await service.DeleteAllEventsAsync();

        var writerTasks = Enumerable.Range(0, 80)
            .Select(i => Task.Run(() => service.LogEvent("test", $"message-{i}")))
            .ToArray();

        var readerTasks = Enumerable.Range(0, 40)
            .Select(async _ =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var entries = await service.GetEventsAsync(limit: 200);
                    Assert.NotNull(entries);
                }
            })
            .ToArray();

        await Task.WhenAll(writerTasks);
        await Task.WhenAll(readerTasks);

        var finalEntries = await service.GetEventsAsync(limit: 200);
        Assert.NotNull(finalEntries);
    }
}
