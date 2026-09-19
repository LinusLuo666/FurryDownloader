using System;

namespace FurryDownloader
{
    public sealed class ArtistEntry
    {
        public ArtistEntry(string name)
        {
            Name = ArtistName.Normalize(name);
        }

        public string Name { get; private set; }
    }

    public static class ArtistName
    {
        public static string Normalize(string input)
        {
            string value = (input ?? String.Empty).Trim().Replace("_", String.Empty);
            if (value.Length == 0)
                throw new ArgumentException("Artist name is required", "input");

            return value;
        }
    }
}
