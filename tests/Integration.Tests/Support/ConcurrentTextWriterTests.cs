namespace Integration.Tests.Support;

using Xunit;

public sealed class ConcurrentTextWriterTests
{
    [Fact]
    public async Task Snapshot_is_safe_while_writes_are_in_flight()
    {
        using ConcurrentTextWriter writer = new();

        Task[] writers = Enumerable.Range(0, 4)
            .Select(writerId => Task.Run(() =>
            {
                for (int line = 0; line < 2_000; line++)
                {
                    writer.WriteLine($"{writerId}:{line}");
                }
            }))
            .ToArray();

        Task[] snapshotters = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() =>
            {
                for (int snapshot = 0; snapshot < 1_000; snapshot++)
                {
                    writer.Snapshot();
                }
            }))
            .ToArray();

        await Task.WhenAll(writers.Concat(snapshotters));

        string[] lines = writer.Snapshot().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(8_000, lines.Length);
    }
}
