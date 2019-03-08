using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;
using System.Threading;

//https://stackoverflow.com/questions/52632448/namedpipeserverstream-readasync-does-not-exit-when-cancellationtoken-requests
/* NamedPipeServerStream class does not implement ReadAsync itself, 
 * it inherits the method from one of its base classes, Stream. It can only detect 
 * cancellation  when the cancel occurred before you call ReadAsync(). Once the read is 
 * started it no longer can see a cancellation. */
public static class AsyncPipeFixer
{
    public static Task<int> ReadPipeAsync(this PipeStream pipe, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<int>(cancellationToken);
        var registration = cancellationToken.Register(() => CancelPipeIo(pipe));
        var async = pipe.BeginRead(buffer, offset, count, null, null);
        return new Task<int>(() => {
            try { return pipe.EndRead(async); }
            finally { registration.Dispose(); }
        }, cancellationToken);
    }

    private static void CancelPipeIo(PipeStream pipe)
    {
        // Note: no PipeStream.IsDisposed, we'll have to swallow
        try
        {
            CancelIo(pipe.SafePipeHandle);
        }
        catch (ObjectDisposedException) { }
    }
    [DllImport("kernel32.dll")]
    private static extern bool CancelIo(SafePipeHandle handle);

}
