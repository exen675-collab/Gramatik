using Gramatik.App.Services;

namespace Gramatik.Tests;

public sealed class AppLoggerTests
{
    [Fact]
    public void Logger_WritesSanitizedEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gramatik-logs-{Guid.NewGuid():N}");
        var logger = new AppLogger(directory);

        logger.Info("TestEvent", "first line\r\nsecond line");

        var log = logger.ReadAll();

        Assert.Contains("TestEvent", log);
        Assert.Contains("first line\\r\\nsecond line", log);
        Assert.DoesNotContain("first line\r\nsecond line", log);
    }
}
