using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FurryDownloader.Tests
{
    public class ArtistListTests
    {
        [Fact]
        public void SaveThenLoadPreservesOrder()
        {
            var path = Path.Combine(Path.GetTempPath(), "furry-artists-" + Guid.NewGuid() + ".xml");
            try
            {
                var store = new ArtistListStore(path);
                store.Save(new[] { new ArtistEntry("Alpha"), new ArtistEntry("Beta") });

                var loaded = store.Load();

                Assert.Equal(new[] { "Alpha", "Beta" }, loaded.Select(x => x.Name));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void BuildRemovesCaseInsensitiveDuplicatesAndKeepsFirstOrder()
        {
            var plan = ArtistUpdatePlan.Build(new[] {
                new ArtistEntry("Alpha"), new ArtistEntry("alpha"), new ArtistEntry("Beta")
            });

            Assert.Equal(new[] { "Alpha", "Beta" }, plan);
        }

        [Fact]
        public void BuildReturnsAnEmptyPlanForAnEmptyList()
        {
            Assert.Empty(ArtistUpdatePlan.Build(Array.Empty<ArtistEntry>()));
        }

        [Fact]
        public void ArtistEntryTrimsAndRemovesUnderscores()
        {
            Assert.Equal("ArtistName", new ArtistEntry(" _Artist_Name_ ").Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizeRejectsBlankArtistNames(string input)
        {
            Assert.Throws<ArgumentException>(() => ArtistName.Normalize(input));
        }
    }
}
