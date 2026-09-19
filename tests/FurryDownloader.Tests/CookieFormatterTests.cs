using Xunit;

namespace FurryDownloader.Tests
{
    public class CookieFormatterTests
    {
        [Fact]
        public void IdentityCookieIsDetectedRegardlessOfCookieOrder()
        {
            FaCookie[] cookies = {
                new FaCookie("b", "session"),
                new FaCookie("a", "identity")
            };

            Assert.True(CookieFormatter.HasIdentityCookie(cookies));
            Assert.Equal("b=session; a=identity", CookieFormatter.ToRequestHeader(cookies));
        }

        [Fact]
        public void EmptyIdentityCookieDoesNotCountAsLoggedIn()
        {
            Assert.False(CookieFormatter.HasIdentityCookie(new[] { new FaCookie("a", "") }));
        }

        [Fact]
        public void EmptyCookieNamesAreIgnored()
        {
            Assert.Equal("a=identity", CookieFormatter.ToRequestHeader(new[] {
                new FaCookie("", "ignored"),
                new FaCookie("a", "identity")
            }));
        }
    }
}
