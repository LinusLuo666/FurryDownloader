namespace FurryDownloader
{
    public sealed class SubmissionInfo
    {
        public SubmissionInfo(string pageUrl, string submissionId, string pictureUrl, string pictureName, string description)
        {
            PageUrl = pageUrl;
            SubmissionId = submissionId;
            PictureUrl = pictureUrl;
            PictureName = pictureName;
            Description = description ?? string.Empty;
        }

        public string PageUrl { get; private set; }
        public string SubmissionId { get; private set; }
        public string PictureUrl { get; private set; }
        public string PictureName { get; private set; }
        public string Description { get; private set; }
    }
}
