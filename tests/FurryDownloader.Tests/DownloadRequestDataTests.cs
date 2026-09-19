using Xunit;

namespace FurryDownloader.Tests
{
    public class DownloadRequestDataTests
    {
        [Fact]
        public void DownloadRequestDataKeepsTheLoginCookie()
        {
            DownloadRequestData request = new DownloadRequestData(
                "https://d.facdn.net/art/a.png", "a=identity; b=session");

            Assert.Equal("https://d.facdn.net/art/a.png", request.Url);
            Assert.Equal("a=identity; b=session", request.Cookie);
        }
    }
}
