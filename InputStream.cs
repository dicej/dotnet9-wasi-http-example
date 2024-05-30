using ProxyWorld.wit.imports.wasi.io.v0_2_0;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

class InputStream: Stream
{
    IStreams.InputStream stream;
    int offset;
    byte[]? buffer;
    bool closed;

    public InputStream(IStreams.InputStream stream) {
        this.stream = stream;
    }

    ~InputStream() {
        Dispose(false);
    }

    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => throw new NotImplementedException();
    public override long Position {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected override void Dispose(bool disposing) {
        stream.Dispose();
    }

    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotImplementedException();
    }

    public override void Flush() {
        // ignore
    }

    public override void SetLength(long length) {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int length) {
        throw new NotImplementedException();        
    }

    public override void Write(byte[] buffer, int offset, int length) {
        throw new NotImplementedException();        
    }

    public override async Task<int> ReadAsync(byte[] bytes, int offset, int length, CancellationToken cancellationToken) {
        while (true) {
            if (closed) {
                return 0;
            } else if (this.buffer == null) {
                // TODO: should we add a special case to the bindings generator
                // to allow passing a buffer to IStreams.InputStream.Read and
                // avoid the extra copy?
                var result = stream.Read(16 * 1024);
                if (result.IsOk) {
                    var buffer = result.AsOk;
                    if (buffer.Length == 0) {
                        await PollTaskScheduler.Instance.Register(stream.Subscribe());
                    } else {
                        this.buffer = buffer;
                        this.offset = 0;
                    }
                } else if (result.AsErr.Tag == IStreams.StreamError.CLOSED) {
                    closed = true;
                    return 0;
                } else {
                    throw new Exception("I/O error");
                }
            } else {
                var min = Math.Min(this.buffer.Length - this.offset, length);
                Array.Copy(this.buffer, this.offset, bytes, offset, min);
                if (min < buffer.Length - this.offset) {
                    this.offset += min;
                } else {
                    this.buffer = null;
                }
                return min;
            }
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        // TODO: avoid copy when possible and use ArrayPool when not
        var dst = new byte[buffer.Length];
        var result = await ReadAsync(dst, 0, buffer.Length, cancellationToken);
        new ReadOnlySpan<byte>(dst, 0, result).CopyTo(buffer.Span);
        return result;
    }
}
