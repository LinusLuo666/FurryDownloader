namespace FurryDownloader
{
    public sealed class FaCookie
    {
        public FaCookie(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Value { get; private set; }
    }
}
