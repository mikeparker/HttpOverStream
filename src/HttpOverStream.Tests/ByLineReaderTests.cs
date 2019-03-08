using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Tests
{
    public class ByLineReaderTests
    {
        [Fact]
        public async Task TestWithCrLf()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\r\nsecond longer line\r\nthird line\r\nhello\r\n\r\n"));
            ms.Flush();
            ms.Position = 0;
            var streamReader = new NormalStreamReader(ms);
            Assert.Equal("first line", await ReadLineAsync(streamReader));
            Assert.Equal("second longer line", await ReadLineAsync(streamReader));
            Assert.Equal("third line", await ReadLineAsync(streamReader));
            Assert.Equal("hello", await ReadLineAsync(streamReader));
            Assert.Equal("", await ReadLineAsync(streamReader));
        }
        [Fact]
        public async Task TestWithLf()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\nsecond longer line\nthird line\nhello\n\n"));
            ms.Flush();
            ms.Position = 0;
            var streamReader = new NormalStreamReader(ms);
            Assert.Equal("first line", await ReadLineAsync(streamReader));
            Assert.Equal("second longer line", await ReadLineAsync(streamReader));
            Assert.Equal("third line", await ReadLineAsync(streamReader));
            Assert.Equal("hello", await ReadLineAsync(streamReader));
            Assert.Equal("", await ReadLineAsync(streamReader));
        }
       
        [Fact]
        public async Task TestMalformedHeaders()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("aaa\nsecond longer line\n"));
            ms.Flush();
            ms.Position = 0;
            var streamReader = new NormalStreamReader(ms);
            Assert.Equal("aaa", await ReadLineAsync(streamReader));
            Assert.Equal("second longer line", await ReadLineAsync(streamReader));
            await Assert.ThrowsAsync<EndOfStreamException>(async ()=> await ReadLineAsync(streamReader));
        }

        private async Task<string> ReadLineAsync(IStreamReader streamReader)
        {
            return await ByLineReader.ReadLineAsync(streamReader, CancellationToken.None);
        }
    }
}
