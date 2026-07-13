using System.Diagnostics;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// A read-only stream wrapper that limits throughput to a specified bytes-per-second rate.
/// Uses a token-bucket approach: tokens accumulate over time and are consumed by reads.
/// </summary>
public sealed class ThrottledStream : Stream
{
    private readonly Stream _inner;
    private readonly long _bytesPerSecond;
    private readonly Stopwatch _stopwatch;
    private long _totalBytesRead;

    /// <param name="inner">The underlying stream to read from.</param>
    /// <param name="bytesPerSecond">Maximum throughput in bytes per second. Must be greater than 0.</param>
    public ThrottledStream(Stream inner, long bytesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bytesPerSecond, 0);
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _bytesPerSecond = bytesPerSecond;
        _stopwatch = Stopwatch.StartNew();
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Throttle().GetAwaiter().GetResult();
        var bytesRead = _inner.Read(buffer, offset, count);
        _totalBytesRead += bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Throttle(cancellationToken);
        var bytesRead = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
        _totalBytesRead += bytesRead;
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Throttle(cancellationToken);
        var bytesRead = await _inner.ReadAsync(buffer, cancellationToken);
        _totalBytesRead += bytesRead;
        return bytesRead;
    }

    private async Task Throttle(CancellationToken cancellationToken = default)
    {
        if (_totalBytesRead <= 0) return;

        var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
        if (elapsedSeconds <= 0) return;

        var allowedBytes = (long)(elapsedSeconds * _bytesPerSecond);
        if (_totalBytesRead >= allowedBytes)
        {
            var waitSeconds = (_totalBytesRead / (double)_bytesPerSecond) - elapsedSeconds;
            if (waitSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(waitSeconds, 1.0)), cancellationToken);
            }
        }
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
