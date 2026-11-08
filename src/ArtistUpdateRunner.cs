using System;
using System.Collections.Generic;

namespace FurryDownloader
{
    public sealed class BatchUpdateSummary
    {
        public int Succeeded { get; internal set; }
        public int Failed { get; internal set; }
        public int Stopped { get; internal set; }
    }

    public static class ArtistUpdateRunner
    {
        public static BatchUpdateSummary Run(
            IEnumerable<string> artists,
            Action<string> updateArtist,
            Func<bool> stopRequested,
            Action<string, Exception> reportFailure)
        {
            if (artists == null) throw new ArgumentNullException("artists");
            if (updateArtist == null) throw new ArgumentNullException("updateArtist");
            if (stopRequested == null) throw new ArgumentNullException("stopRequested");
            if (reportFailure == null) throw new ArgumentNullException("reportFailure");

            BatchUpdateSummary summary = new BatchUpdateSummary();
            foreach (string artist in artists)
            {
                if (stopRequested())
                {
                    summary.Stopped++;
                    break;
                }

                try
                {
                    updateArtist(artist);
                    if (stopRequested())
                    {
                        summary.Stopped++;
                        break;
                    }
                    summary.Succeeded++;
                }
                catch (Exception error)
                {
                    if (stopRequested())
                    {
                        summary.Stopped++;
                        break;
                    }
                    summary.Failed++;
                    reportFailure(artist, error);
                }
            }

            return summary;
        }
    }
}
