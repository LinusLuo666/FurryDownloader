using System;
using System.IO;
using System.Text;

namespace FurryDownloader
{
    public static class DescriptionWriter
    {
        public static string GetPath(string artistDirectory, string imageName, string submissionId)
        {
            if (String.IsNullOrWhiteSpace(artistDirectory))
                throw new ArgumentException("Artist directory is required", "artistDirectory");

            string safeImageName = Path.GetFileName(imageName ?? String.Empty);
            string baseName = Path.GetFileNameWithoutExtension(safeImageName);
            if (String.IsNullOrWhiteSpace(baseName))
                baseName = "submission";

            string directory = Path.Combine(artistDirectory, "descriptions");
            string path = Path.Combine(directory, baseName + ".txt");
            if (!File.Exists(path))
                return path;

            string safeId = SanitizeFilePart(submissionId);
            if (safeId.Length == 0)
                safeId = "duplicate";

            return Path.Combine(directory, baseName + "." + safeId + ".txt");
        }

        public static void Write(string path, string description)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Description path is required", "path");

            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, description ?? String.Empty, new UTF8Encoding(false));
        }

        private static string SanitizeFilePart(string value)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;

            StringBuilder result = new StringBuilder();
            foreach (char character in value)
            {
                if (Char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    result.Append(character);
            }

            return result.ToString();
        }
    }
}
