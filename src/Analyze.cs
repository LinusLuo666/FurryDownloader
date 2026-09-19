using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/**
 * 分析网页内容并提取指定内容的类
 */
namespace FurryDownloader
{
    public class Analyze
    {
        /// <summary>
        /// 检查是否存在此用户名
        /// </summary>
        public static bool HasUser(string content)
        {
            return !Regex.IsMatch(content, @"The username ""\w+"" could not be found.");
        }

        /// <summary>
        /// 检查是否需要登录后查看
        /// </summary>
        public static bool NeedLogin(string content)
        {
            return !content.Contains("has elected to make their content available to registered users only.");
        }

        /// <summary>
        /// 分析出当前页面里所有的详情页地址
        /// </summary>
        public static List<string> GetPages(string content)
        {
            List<string> pages = new List<string>();
            MatchCollection matchs = Regex.Matches(content, @"<a href=""/view/\d+/"">");
            foreach (Match match in matchs)
                pages.Add("https://www.furaffinity.net" + match.Value.Split('"')[1]);

            return pages;
        }

        /// <summary>
        /// 提取详情页内的原图下载地址
        /// </summary>
        public static string GetPictureUrl(string page)
        {
            MatchCollection matchs = Regex.Matches(page, @"href="".*?"".*?>Download");
            if (matchs.Count == 0)
                return null;
            else
                return "https:" + matchs[0].Value.Split('"')[1];
        }

        /// <summary>
        /// Extracts the image and description from one submission page.
        /// </summary>
        public static SubmissionInfo GetSubmissionInfo(string pageUrl, string page)
        {
            Match pictureMatch = Regex.Match(
                page ?? String.Empty,
                @"href\s*=\s*""(?<url>(?:https?:)?//[^""]+)""[^>]*>\s*Download",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!pictureMatch.Success)
                throw new InvalidOperationException("无法分析出图片地址：" + pageUrl);

            string pictureUrl = pictureMatch.Groups["url"].Value;
            if (pictureUrl.StartsWith("//", StringComparison.Ordinal))
                pictureUrl = "https:" + pictureUrl;

            Match idMatch = Regex.Match(pageUrl ?? String.Empty, @"/view/(?<id>\d+)/?", RegexOptions.IgnoreCase);
            string submissionId = idMatch.Success ? idMatch.Groups["id"].Value : "unknown";
            string description = GetDescription(page);

            return new SubmissionInfo(
                pageUrl,
                submissionId,
                pictureUrl,
                GetFileName(pictureUrl),
                description);
        }

        private static string GetDescription(string page)
        {
            Match descriptionMatch = Regex.Match(
                page ?? String.Empty,
                @"<div[^>]*class=""[^""]*\bsubmission-description-text\b[^""]*""[^>]*>(?<body>.*?)</div>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!descriptionMatch.Success)
                return String.Empty;

            string fragment = descriptionMatch.Groups["body"].Value;
            fragment = Regex.Replace(fragment, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            fragment = Regex.Replace(fragment, @"<[^>]+>", String.Empty, RegexOptions.Singleline);
            fragment = WebUtility.HtmlDecode(fragment);
            fragment = fragment.Replace("\r\n", "\n").Replace("\r", "\n");
            return Regex.Replace(fragment, @"[ \t]+\n", "\n").Trim();
        }

        /// <summary>
        /// 提取原图下载地址内的文件名信息
        /// </summary>
        public static string GetFileName(string downloadUrl)
        {
            string[] result = downloadUrl.Split('/');
            return result[result.Length - 1];
        }
    }
}
