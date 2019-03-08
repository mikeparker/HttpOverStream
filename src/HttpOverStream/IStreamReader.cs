using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public interface IStreamReader
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }
}
