using System;
using System.Collections.Generic;
using Xunit;

namespace FurryDownloader.Tests
{
    public class BatchUpdateRunnerTests
    {
        [Fact]
        public void RunnerContinuesAfterOneArtistFails()
        {
            List<string> processed = new List<string>();
            BatchUpdateSummary result = ArtistUpdateRunner.Run(
                new[] { "first", "bad", "second" },
                artist =>
                {
                    processed.Add(artist);
                    if (artist == "bad") throw new InvalidOperationException("boom");
                },
                () => false,
                (artist, error) => { });

            Assert.Equal(new[] { "first", "bad", "second" }, processed);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(1, result.Failed);
        }

        [Fact]
        public void RunnerStopsBeforeTheNextArtistWhenRequested()
        {
            List<string> processed = new List<string>();
            BatchUpdateSummary result = ArtistUpdateRunner.Run(
                new[] { "first", "second" },
                artist => processed.Add(artist),
                () => processed.Count == 1,
                (artist, error) => { });

            Assert.Single(processed);
            Assert.Equal(1, result.Stopped);
        }
    }
}
