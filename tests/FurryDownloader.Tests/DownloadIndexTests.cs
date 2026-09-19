using System;
using System.IO;
using Xunit;

namespace FurryDownloader.Tests
{
    public class DownloadIndexTests
    {
        [Fact]
        public void RecordedUrlCanBeLoadedAndResolvedToRelativePath()
        {
            string path = Path.Combine(Path.GetTempPath(), "furry-index-" + Guid.NewGuid() + ".xml");
            try
            {
                DownloadIndex index = DownloadIndex.Load(path);
                index.Record("https://d.facdn.net/art/a.png", "Artist\\a.png");
                index.Save();

                DownloadIndex reloaded = DownloadIndex.Load(path);
                string relative;
                Assert.True(reloaded.TryGetPath("https://d.facdn.net/art/a.png", out relative));
                Assert.Equal("Artist\\a.png", relative);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void ProtocolRelativeUrlIsNormalized()
        {
            string path = Path.Combine(Path.GetTempPath(), "furry-index-" + Guid.NewGuid() + ".xml");
            try
            {
                DownloadIndex index = DownloadIndex.Load(path);
                index.Record(" //d.facdn.net/art/a.png ", "Artist\\a.png");

                string relative;
                Assert.True(index.TryGetPath("https://d.facdn.net/art/a.png", out relative));
                Assert.Equal("Artist\\a.png", relative);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void MalformedIndexIsBackedUpAndDoesNotBlockLoading()
        {
            string path = Path.Combine(Path.GetTempPath(), "furry-index-" + Guid.NewGuid() + ".xml");
            try
            {
                File.WriteAllText(path, "<downloads>");

                DownloadIndex index = DownloadIndex.Load(path);

                Assert.False(index.TryGetPath("https://d.facdn.net/art/a.png", out _));
                Assert.True(File.Exists(path + ".bak"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }

        [Fact]
        public void RecordingSameUrlReplacesOneMapping()
        {
            string path = Path.Combine(Path.GetTempPath(), "furry-index-" + Guid.NewGuid() + ".xml");
            try
            {
                DownloadIndex index = DownloadIndex.Load(path);
                index.Record("https://d.facdn.net/art/a.png", "Artist\\old.png");
                index.Record("https://d.facdn.net/art/a.png", "Artist\\new.png");

                string relative;
                Assert.True(index.TryGetPath("https://d.facdn.net/art/a.png", out relative));
                Assert.Equal("Artist\\new.png", relative);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
