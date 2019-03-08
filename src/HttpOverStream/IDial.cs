using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public interface IDial
    {
        ValueTask<(Stream, IStreamReader)> DialAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    }
}
