using System;
using Xunit;

namespace FurryDownloader.Tests
{
    public class AnalyzeTests
    {
        private const string DetailHtml = @"
<a href=""//d.facdn.net/art/artist/1/work.png"">Download</a>
<div class=""submission-description-text user-submitted-links"">
  Hello &amp; welcome<br />
  <strong>Second line</strong>
</div>";

        [Fact]
        public void GetSubmissionInfoExtractsImageAndPlainTextDescription()
        {
            SubmissionInfo info = Analyze.GetSubmissionInfo(
                "https://www.furaffinity.net/view/123/", DetailHtml);

            Assert.Equal("123", info.SubmissionId);
            Assert.Equal("https://d.facdn.net/art/artist/1/work.png", info.PictureUrl);
            Assert.Equal("work.png", info.PictureName);
            Assert.Contains("Hello & welcome", info.Description);
            Assert.Contains("Second line", info.Description);
        }

        [Fact]
        public void GetSubmissionInfoKeepsAbsoluteImageUrlAndAllowsMissingDescription()
        {
            SubmissionInfo info = Analyze.GetSubmissionInfo(
                "https://www.furaffinity.net/view/124/",
                @"<a href=""https://d.facdn.net/art/artist/1/work.jpg"">Download</a>");

            Assert.Equal("https://d.facdn.net/art/artist/1/work.jpg", info.PictureUrl);
            Assert.Equal(String.Empty, info.Description);
        }
    }
}
