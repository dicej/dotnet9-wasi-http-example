using ProxyWorld.wit.imports.wasi.io.v0_2_0;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

class OutputStream: Stream
{
    IStreams.OutputStream stream;

    public OutputStream(IStreams.OutputStream stream) {
        this.stream = stream;
    }

    ~OutputStream() {
        Dispose(false);
    }
    
    public override bool CanRead => false;
    public override bool CanWrite => true;
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

    public override async Task WriteAsync(byte[] bytes, int offset, int length, CancellationToken cancellationToken) {
        var limit = offset + length;
        var flushing = false;
        while (true) {
            var count = (int) stream.CheckWrite().AsOk;
            if (count == 0) {
                await PollTaskScheduler.Instance.Register(stream.Subscribe());
            } else if (offset == limit) {
                if (flushing) {
                    return;
                } else {
                    stream.Flush();
                    flushing = true;
                }
            } else {
                var min = Math.Min(count, limit - offset);
                if (offset == 0 && min == bytes.Length) {
                    stream.Write(bytes);
                } else {
                    // TODO: is there a more efficient option than copying here?
                    // Do we need to change the binding generator to accept
                    // e.g. `Span`s?
                    var copy = new byte[min];
                    Array.Copy(bytes, offset, copy, 0, min);
                    stream.Write(copy);
                }
                offset += min;
            }
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        // TODO: avoid copy when possible and use ArrayPool when not
        var copy = new byte[buffer.Length];
        buffer.Span.CopyTo(copy);
        return new ValueTask(WriteAsync(copy, 0, buffer.Length, cancellationToken));
    }
}
