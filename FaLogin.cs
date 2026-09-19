using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace FurryDownloader
{
    public partial class FaLogin : Form
    {
        private const string LoginUrl = "https://www.furaffinity.net/login/?mode=imagecaptcha";

        // 登录后的身份 cookie
        public string Cookie = "";

        public FaLogin()
        {
            InitializeComponent();
            Shown += FaLogin_Shown;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
        }

        private async void FaLogin_Shown(object sender, EventArgs e)
        {
            await InitializeBrowserAsync();
        }

        private async System.Threading.Tasks.Task InitializeBrowserAsync()
        {
            string dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "FaDownloader",
                "WebView2");

            try
            {
                Directory.CreateDirectory(dataDirectory);
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                    null,
                    dataDirectory,
                    null);
                await WebView.EnsureCoreWebView2Async(environment);
                WebView.CoreWebView2.Navigate(LoginUrl);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(
                    "未找到 Microsoft Edge WebView2 Runtime，请安装后重新登录。",
                    "登录失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    "无法初始化登录页面：" + error.Message,
                    "登录失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private async void WebView_NavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || WebView.CoreWebView2 == null || !String.IsNullOrEmpty(Cookie))
                return;

            try
            {
                var cookies = await WebView.CoreWebView2.CookieManager
                    .GetCookiesAsync("https://www.furaffinity.net/");
                var faCookies = cookies
                    .Select(cookie => new FaCookie(cookie.Name, cookie.Value))
                    .ToList();

                if (CookieFormatter.HasIdentityCookie(faCookies))
                {
                    Cookie = CookieFormatter.ToRequestHeader(faCookies);
                    Close();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("读取 FA 登录 Cookie 失败：" + error.Message);
            }
        }
    }
}
