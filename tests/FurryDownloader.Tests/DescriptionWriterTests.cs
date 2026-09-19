using System;
using System.IO;
using System.Text;
using Xunit;

namespace FurryDownloader.Tests
{
    public class DescriptionWriterTests
    {
        [Fact]
        public void GetPathUsesDescriptionsFolderAndImageBaseName()
        {
            string root = Path.Combine(Path.GetTempPath(), "furry-description-" + Guid.NewGuid());
            try
            {
                string path = DescriptionWriter.GetPath(root, "work.png", "123");

                Assert.Equal(Path.Combine(root, "descriptions", "work.txt"), path);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public void ExistingDescriptionGetsSubmissionIdSuffix()
        {
            string root = Path.Combine(Path.GetTempPath(), "furry-description-" + Guid.NewGuid());
            try
            {
                string directory = Path.Combine(root, "descriptions");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "work.txt"), "old");

                string path = DescriptionWriter.GetPath(root, "work.png", "123");

                Assert.Equal(Path.Combine(directory, "work.123.txt"), path);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public void WriteStoresUtf8Text()
        {
            string path = Path.Combine(Path.GetTempPath(), "furry-description-" + Guid.NewGuid(), "work.txt");
            try
            {
                DescriptionWriter.Write(path, "中文\nsecond");

                Assert.Equal("中文\nsecond", File.ReadAllText(path, Encoding.UTF8));
            }
            finally
            {
                string root = Path.GetDirectoryName(path);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
