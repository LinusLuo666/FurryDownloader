using System;
using System.Collections.Generic;
using System.Linq;

namespace FurryDownloader
{
    public static class CookieFormatter
    {
        public static bool HasIdentityCookie(IEnumerable<FaCookie> cookies)
        {
            return cookies != null && cookies.Any(cookie =>
                cookie != null && String.Equals(cookie.Name, "a", StringComparison.Ordinal) &&
                !String.IsNullOrEmpty(cookie.Value));
        }

        public static string ToRequestHeader(IEnumerable<FaCookie> cookies)
        {
            if (cookies == null)
                return String.Empty;

            return String.Join("; ", cookies
                .Where(cookie => cookie != null && !String.IsNullOrEmpty(cookie.Name))
                .Select(cookie => cookie.Name + "=" + cookie.Value));
        }
    }
}
