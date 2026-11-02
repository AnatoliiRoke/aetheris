namespace Aetheris.Host.Observability;

/// <summary>
/// Keeps the first bytes of a body that passes through a stream and counts all of them.
/// </summary>
/// <remarks>
/// The body is never copied as a whole: at most <c>limit</c> bytes are kept, and the stream
/// only forwards the calls to the stream under it.
/// </remarks>
public sealed class BodyCapture
{
    private const int GrowthFactor = 2;

    private readonly int _limit;
    private byte[] _buffer = [];
    private int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="BodyCapture"/> class.
    /// </summary>
    /// <param name="limit">The largest number of bytes to keep.</param>
    public BodyCapture(int limit)
    {
        _limit = limit;
    }

    /// <summary>
    /// Gets the number of bytes that passed through.
    /// </summary>
    public long TotalBytes { get; private set; }

    /// <summary>
    /// Gets the bytes that were kept.
    /// </summary>
    public ReadOnlySpan<byte> Captured => _buffer.AsSpan(0, _length);

    /// <summary>
    /// Counts bytes of a body that is not kept.
    /// </summary>
    /// <param name="count">The number of bytes that passed through.</param>
    public void AddCountOnly(int count) => TotalBytes += count;

    /// <summary>
    /// Counts a piece of the body and keeps its part that still fits under the limit.
    /// </summary>
    /// <param name="piece">The piece that passed through.</param>
    public void Add(ReadOnlySpan<byte> piece)
    {
        TotalBytes += piece.Length;

        int room = _limit - _length;

        if (room <= 0 || piece.IsEmpty)
        {
            return;
        }

        ReadOnlySpan<byte> kept = piece[..Math.Min(room, piece.Length)];

        if (_buffer.Length < _length + kept.Length)
        {
            Array.Resize(ref _buffer, Math.Min(_limit, Math.Max(_length + kept.Length, _buffer.Length * GrowthFactor)));
        }

        kept.CopyTo(_buffer.AsSpan(_length));
        _length += kept.Length;
    }
}

/// <summary>
/// A read-only stream that passes the request body on and tells a <see cref="BodyCapture"/> what it read.
/// </summary>
public sealed class CaptureReadStream : Stream
{
    private readonly Stream _inner;
    private readonly BodyCapture _capture;

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptureReadStream"/> class.
    /// </summary>
    /// <param name="inner">The stream of the request body.</param>
    /// <param name="capture">Where the bytes that were read are noted.</param>
    public CaptureReadStream(Stream inner, BodyCapture capture)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(capture);

        _inner = inner;
        _capture = capture;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        _capture.Add(buffer.AsSpan(offset, read));

        return read;
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        _capture.Add(buffer[..read]);

        return read;
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _capture.Add(buffer.AsSpan(offset, read));

        return read;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _capture.Add(buffer.Span[..read]);

        return read;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// A write-only stream that passes the response body on and tells a <see cref="BodyCapture"/>
/// what was written, when the response is of a type that is logged.
/// </summary>
/// <remarks>
/// Whether the body is kept is decided at the first write, when the headers of the response are
/// known. A response that is not logged is only counted.
/// </remarks>
public sealed class CaptureWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly BodyCapture _capture;
    private readonly Func<bool> _isLogged;
    private bool? _keep;

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptureWriteStream"/> class.
    /// </summary>
    /// <param name="inner">The stream of the response body.</param>
    /// <param name="capture">Where the bytes that were written are noted.</param>
    /// <param name="isLogged">Tells at the first write whether the body of this response is kept.</param>
    public CaptureWriteStream(Stream inner, BodyCapture capture, Func<bool> isLogged)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(isLogged);

        _inner = inner;
        _capture = capture;
        _isLogged = isLogged;
    }

    /// <summary>
    /// Gets a value indicating whether the body of the response was kept.
    /// </summary>
    public bool Kept => _keep == true;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        Note(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Note(buffer);
        _inner.Write(buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Note(buffer.AsSpan(offset, count));

        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Note(buffer.Span);

        return _inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private void Note(ReadOnlySpan<byte> piece)
    {
        _keep ??= _isLogged();

        if (_keep == true)
        {
            _capture.Add(piece);
        }
        else
        {
            _capture.AddCountOnly(piece.Length);
        }
    }
}
