using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FurryDownloader
{
    public sealed class ArtistListStore
    {
        private readonly string path;

        public ArtistListStore(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A file path is required", "path");

            this.path = path;
        }

        public IList<ArtistEntry> Load()
        {
            if (!File.Exists(path))
                return new List<ArtistEntry>();

            try
            {
                XDocument document = XDocument.Load(path);
                XElement root = document.Root;
                if (root == null || root.Name != "artists")
                    return new List<ArtistEntry>();

                return root.Elements("artist")
                    .Select(element => new ArtistEntry(element.Value))
                    .ToList();
            }
            catch (Exception error) when (error is XmlException ||
                                          error is IOException ||
                                          error is UnauthorizedAccessException ||
                                          error is ArgumentException)
            {
                TryBackupCorruptFile();
                return new List<ArtistEntry>();
            }
        }

        public void Save(IEnumerable<ArtistEntry> artists)
        {
            if (artists == null)
                throw new ArgumentNullException("artists");

            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            XElement root = new XElement("artists",
                artists.Select(artist => new XElement("artist", artist.Name)));
            XDocument document = new XDocument(root);
            string temporaryPath = path + ".tmp";

            document.Save(temporaryPath, SaveOptions.None);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }

        private void TryBackupCorruptFile()
        {
            try
            {
                File.Copy(path, path + ".bak", true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
