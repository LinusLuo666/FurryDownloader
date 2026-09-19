using System;

namespace FurryDownloader
{
    public sealed class DownloadRequestData
    {
        public DownloadRequestData(string url, string cookie)
        {
            Url = url ?? String.Empty;
            Cookie = cookie ?? String.Empty;
        }

        public string Url { get; private set; }
        public string Cookie { get; private set; }
    }
}
