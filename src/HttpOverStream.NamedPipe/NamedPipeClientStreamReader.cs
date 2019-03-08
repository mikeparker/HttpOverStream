using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeClientStreamReader : IStreamReader
    {
        private NamedPipeClientStream _stream;

        public NamedPipeClientStreamReader(NamedPipeClientStream stream)
        {
            _stream = stream;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadPipeAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        /* Taken from:
         * https://stackoverflow.com/questions/52632448/namedpipeserverstream-readasync-does-not-exit-when-cancellationtoken-requests
         * NamedPipeServerStream class does not implement ReadAsync itself, 
         * it inherits the method from one of its base classes, Stream. It can only detect 
         * cancellation  when the cancel occurred before you call ReadAsync(). Once the read is 
         * started it no longer can see a cancellation. */
        public Task<int> ReadPipeAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<int>(cancellationToken);
            var registration = cancellationToken.Register(() => CancelPipeIo(_stream));
            var async = _stream.BeginRead(buffer, offset, count, null, null);
            return new Task<int>(() => {
                try { return _stream.EndRead(async); }
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
}