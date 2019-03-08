using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class NormalStreamReader : IStreamReader
    {
        private readonly Stream _stream;

        public NormalStreamReader(Stream stream)
        {
            _stream = stream;
        }
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await _stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }
    }
}
