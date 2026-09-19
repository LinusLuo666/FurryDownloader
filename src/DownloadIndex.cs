using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace FurryDownloader
{
    public sealed class DownloadIndex
    {
        private readonly string path;
        private readonly Dictionary<string, string> entries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private DownloadIndex(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Index path is required", "path");

            this.path = path;
        }

        public static DownloadIndex Load(string path)
        {
            DownloadIndex index = new DownloadIndex(path);
            if (!File.Exists(path))
                return index;

            try
            {
                XDocument document = XDocument.Load(path);
                XElement root = document.Root;
                if (root == null || root.Name != "downloads")
                    return index;

                foreach (XElement item in root.Elements("item"))
                {
                    XAttribute url = item.Attribute("url");
                    XAttribute relativePath = item.Attribute("path");
                    if (url != null && relativePath != null)
                    {
                        string normalizedUrl = NormalizeUrl(url.Value);
                        if (normalizedUrl.Length > 0 && relativePath.Value.Length > 0)
                            index.entries[normalizedUrl] = relativePath.Value;
                    }
                }

                return index;
            }
            catch (Exception error) when (error is XmlException ||
                                          error is IOException ||
                                          error is UnauthorizedAccessException ||
                                          error is ArgumentException)
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

                return index;
            }
        }

        public bool TryGetPath(string url, out string relativePath)
        {
            string normalizedUrl = NormalizeUrl(url);
            if (normalizedUrl.Length == 0)
            {
                relativePath = null;
                return false;
            }

            return entries.TryGetValue(normalizedUrl, out relativePath);
        }

        public void Record(string url, string relativePath)
        {
            string normalizedUrl = NormalizeUrl(url);
            if (normalizedUrl.Length == 0)
                throw new ArgumentException("Download URL is required", "url");
            if (String.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is required", "relativePath");

            entries[normalizedUrl] = relativePath;
        }

        public void Save()
        {
            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            XElement root = new XElement("downloads");
            foreach (KeyValuePair<string, string> entry in entries)
            {
                root.Add(new XElement("item",
                    new XAttribute("url", entry.Key),
                    new XAttribute("path", entry.Value)));
            }

            string temporaryPath = path + ".tmp";
            new XDocument(root).Save(temporaryPath, SaveOptions.None);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }

        private static string NormalizeUrl(string url)
        {
            string value = (url ?? String.Empty).Trim();
            if (value.StartsWith("//", StringComparison.Ordinal))
                value = "https:" + value;

            return value;
        }
    }
}
