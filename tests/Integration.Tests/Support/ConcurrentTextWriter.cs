namespace Integration.Tests.Support;

using System.Text;

internal sealed class ConcurrentTextWriter : TextWriter
{
    private readonly StringBuilder buffer = new();
    private readonly Lock sync = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (this.sync)
        {
            this.buffer.Append(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        lock (this.sync)
        {
            this.buffer.Append(buffer, index, count);
        }
    }

    public override void Write(string? value)
    {
        lock (this.sync)
        {
            this.buffer.Append(value);
        }
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        lock (this.sync)
        {
            this.buffer.Append(buffer);
        }
    }

    public override void WriteLine()
    {
        lock (this.sync)
        {
            this.buffer.AppendLine();
        }
    }

    public override void WriteLine(string? value)
    {
        lock (this.sync)
        {
            this.buffer.AppendLine(value);
        }
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        lock (this.sync)
        {
            this.buffer.Append(buffer);
            this.buffer.AppendLine();
        }
    }

    public string Snapshot()
    {
        lock (this.sync)
        {
            return this.buffer.ToString();
        }
    }
}
