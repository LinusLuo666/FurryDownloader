using System;
using System.Collections.Generic;

namespace FurryDownloader
{
    public static class ArtistUpdatePlan
    {
        public static IList<string> Build(IEnumerable<ArtistEntry> artists)
        {
            if (artists == null)
                throw new ArgumentNullException("artists");

            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ArtistEntry artist in artists)
            {
                if (artist != null && seen.Add(artist.Name))
                    result.Add(artist.Name);
            }

            return result;
        }
    }
}
