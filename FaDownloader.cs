using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

namespace FurryDownloader
{
    public partial class mainFrom : Form
    {
        //基本参数
        private string userName;            // 作者名
        private string filePath;            // 下载存放目录
        private string cookie = "";         // cookie
        private string downloadRootDirectory;

        private ArtistListStore artistStore;
        private List<ArtistEntry> artists = new List<ArtistEntry>();
        private DownloadIndex downloadIndex;
        private readonly object downloadIndexLock = new object();
        private readonly object queuedDownloadLock = new object();
        private readonly HashSet<string> queuedDownloadUrls =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        //高级参数
        private int startPageNum = 1;       // 下载起始页的页号，默认1
        private int startPicNum = 1;        // 从当前页的第几张开始下载，默认1

        private int totalDownloadNum = 0;   // 已下载的图片个数
        private int skipDownloadNum = 0;    // 已跳过下载的图片个数
        private int maxDownloadNum = 0;     // 最大下载量
        private int currentDownloadTaskNum = 0; // 当前已添加的下载任务数量

        private Thread workerThread;        // 主循环线程
        private DateTime startTime;         // 开始下载前的时间

        private volatile bool isStop = false;        // 是否停止下载
        private bool retryOnNetworkErrors = true;

        private HttpHelper http = new HttpHelper();
        private string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\FaDownloader\\";

        public mainFrom()
        {
            InitializeComponent();
            LoadCookie();
            LoadSetting();

            artistStore = new ArtistListStore(Path.Combine(homeDir, "artists.xml"));
            artists = new List<ArtistEntry>(artistStore.Load());
            downloadIndex = DownloadIndex.Load(Path.Combine(homeDir, "download-index.xml"));
            RefreshArtistList();
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="dir"></param>
        private void CreateDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }


        /// <summary>
        /// 创建文件
        /// </summary>
        /// <param name="path"></param>
        private void CreateFile(string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }
        }


        #region cookie文件函数
        /// <summary>
        /// 读取cookie
        /// </summary>
        private void LoadCookie()
        {
            string cookiePath = homeDir + "cookie.txt";
            CreateDir(homeDir);

            //如果已经存在cookie文件则读取
            if (File.Exists(cookiePath))
            {
                cookie = File.ReadAllText(cookiePath, Encoding.UTF8);
            }
        }
        /// <summary>
        /// 保存cookie
        /// </summary>
        private void SaveCookie(string cookie)
        {
            //为空则不存储
            if (cookie == "")
                return;

            // 创建并写入
            string path = homeDir + "cookie.txt";
            CreateDir(homeDir);
            CreateFile(path);
            File.WriteAllText(path, cookie, Encoding.UTF8);
        }
        #endregion
        #region 配置文件
        private void LoadSetting()
        {
            string settingPath = homeDir + "setting.txt";
            CreateDir(homeDir);

            // 如果已经存在设置文件则读取
            // 暂时只存储路径
            if (File.Exists(settingPath))
            {
                filePath = File.ReadAllText(settingPath, Encoding.UTF8);
                FilePath.Text = filePath;
            }
        }

        private void SaveSetting(string setting)
        {
            if (filePath == "")
            {
                return;
            }

            // 创建并写入
            string path = homeDir + "setting.txt";
            CreateDir(homeDir);
            CreateFile(path);
            File.WriteAllText(path, setting, Encoding.UTF8);
        }
        #endregion
        #region 操作控件的工具类
        delegate void TextBoxDelegate(string str);
        /// <summary>在textBox中追加信息，因为在其他线程无法操作主线程的控件，所以需要用这种方法<</summary>
        /// <param name="str">要追加的信息</param>
        public void AddItemToTextBox(string str)
        {
            try
            {
                if (textBox.InvokeRequired)
                {
                    TextBoxDelegate d = AddItemToTextBox;
                    textBox.Invoke(d, str);
                }
                else
                {
                    textBox.AppendText(str + "\r\n");
                }
            }
            catch
            {

            }
        }
        delegate void FreezeDelegate();
        /// <summary>
        /// 冻结
        /// </summary>
        public void Freeze()
        {
            try
            {
                if (ButtonEnter.InvokeRequired)
                {
                    FreezeDelegate d = Freeze;
                    ButtonEnter.Invoke(d);
                }
                else
                {
                    //冻结所有控件
                    ButtonEnter.Enabled = false;
                    ButtonUpdateList.Enabled = false;
                    ButtonCancle.Enabled = true;
                    input_name.Enabled = false;
                    artistList.Enabled = false;
                    ButtonAddArtist.Enabled = false;
                    ButtonRemoveArtist.Enabled = false;
                    RadioButtonGallery.Enabled = RadioButtonScraps.Enabled = false;
                    Browse.Enabled = false;
                    InputPageNum.Enabled = InputStartPicNum.Enabled = InputMaxDownloadNum.Enabled = false;
                }
            }
            catch
            {

            }
        }

        delegate void UnFreezeDelegate();
        /// <summary>
        /// 解冻
        /// </summary>
        public void UnFreeze()
        {
            try
            {
                if (ButtonEnter.InvokeRequired)
                {
                    UnFreezeDelegate d = UnFreeze;
                    ButtonEnter.Invoke(d);
                }
                else
                {
                    //解冻所有控件
                    ButtonEnter.Enabled = true;
                    ButtonUpdateList.Enabled = true;
                    ButtonCancle.Enabled = false;
                    input_name.Enabled = true;
                    artistList.Enabled = true;
                    ButtonAddArtist.Enabled = true;
                    ButtonRemoveArtist.Enabled = true;
                    RadioButtonGallery.Enabled = RadioButtonScraps.Enabled = true;
                    Browse.Enabled = true;
                    InputPageNum.Enabled = InputStartPicNum.Enabled = InputMaxDownloadNum.Enabled = true;
                }
            }
            catch
            {

            }
        }
        #endregion
        #region 按钮回调

        /// <summary>
        /// 确定按钮触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonStart_Click(object sender, EventArgs e)
        {
            ButtonUpdateList_Click(sender, e);
        }

        private void ButtonAddArtist_Click(object sender, EventArgs e)
        {
            try
            {
                ArtistEntry entry = new ArtistEntry(input_name.Text);
                foreach (ArtistEntry artist in artists)
                {
                    if (String.Equals(artist.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        AddItemToTextBox("画师已在列表中：" + entry.Name);
                        return;
                    }
                }

                artists.Add(entry);
                artistStore.Save(artists);
                RefreshArtistList();
                input_name.Clear();
                AddItemToTextBox("已添加画师：" + entry.Name);
            }
            catch (ArgumentException error)
            {
                AddItemToTextBox("无法添加画师：" + error.Message);
            }
        }

        private void ButtonRemoveArtist_Click(object sender, EventArgs e)
        {
            if (artistList.SelectedIndex < 0 || artistList.SelectedIndex >= artists.Count)
            {
                AddItemToTextBox("请先选择要删除的画师");
                return;
            }

            string removedName = artists[artistList.SelectedIndex].Name;
            artists.RemoveAt(artistList.SelectedIndex);
            artistStore.Save(artists);
            RefreshArtistList();
            AddItemToTextBox("已删除画师：" + removedName);
        }

        private void ButtonUpdateList_Click(object sender, EventArgs e)
        {
            if (workerThread != null && workerThread.IsAlive)
                return;

            // 在 UI 线程读取选项，后台线程只使用快照，避免跨线程访问 WinForms 控件。
            GetUserInputOptionsForBatch();
            isStop = false;
            workerThread = new Thread(UpdateArtistList);
            workerThread.Start();
        }

        /// <summary>
        /// 取消按钮触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonCancle_Click(object sender, EventArgs e)
        {
            isStop = true;
        }

        /// <summary>
        /// 浏览按钮触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = folderBrowserDialog.SelectedPath;
                FilePath.Text = filePath;
                // 保存设置
                SaveSetting(filePath);
            }
        }

        /// <summary>
        /// 登录fa
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonLogin_Click(object sender, EventArgs e)
        {
            FaLogin faLogin = new FaLogin();
            faLogin.ShowDialog();
            if (faLogin.Cookie != null && faLogin.Cookie != "")
            {
                SaveCookie(faLogin.Cookie);
                LoadCookie();
                MessageBox.Show("登录成功，已保存身份信息以供下次使用");
            }
            faLogin.Dispose();
        }

        /// <summary>
        /// 帮助按钮触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Help_Click(object sender, EventArgs e)
        {
            string help = "本软件用于批量下载fa站图片\r\n" +
                        "请保证网络连接通畅\r\n" +
                        "必要时请给于软件管理员权限\r\n" +
                        "下载图片不成功时，请开启vpn后重新下载\r\n" +
                        "本软件仅供交流，请勿用于商业用途\r\n";

            MessageBox.Show(help, "注意事项");
        }
        #endregion
        #region 初始化函数和收尾函数

        /// <summary>
        /// 获取用户提供的数据
        /// </summary>
        private void GetUserInput()
        {
            // 获取画师名
            userName = input_name.Text.Trim().Replace("_", "");

            // 获取下载目录
            downloadRootDirectory = GetDownloadRootDirectory();
            filePath = Path.Combine(downloadRootDirectory, userName);

            ReadDownloadOptions();
        }

        private void ReadDownloadOptions()
        {

            // 处理高级选项参数
            string inputPage = InputPageNum.Text.Trim();
            string inputStartPic = InputStartPicNum.Text.Trim();
            string inputMaxDownloadNum = InputMaxDownloadNum.Text.Trim();

            // 起始页数，默认值为1
            if (!int.TryParse(inputPage, out startPageNum) || startPageNum < 1)
                this.startPageNum = 1;

            // 从第几张开始下载，默认值为1
            if (!int.TryParse(inputStartPic, out startPicNum) || startPicNum < 1)
                this.startPicNum = 1;

            // 最大下载量，默认为0，即不限量
            if (!int.TryParse(inputMaxDownloadNum, out maxDownloadNum) || maxDownloadNum < 0)
                this.maxDownloadNum = 0;

            // 输出高级参数下载信息
            AddItemToTextBox(string.Format("从第{0}页第{1}张开始下载\r\n", this.startPageNum, this.startPicNum));
        }

        private string GetDownloadRootDirectory()
        {
            string root = (FilePath.Text ?? String.Empty).Trim();
            return root.Length == 0 ? Directory.GetCurrentDirectory() : root;
        }

        private void RefreshArtistList()
        {
            artistList.Items.Clear();
            foreach (ArtistEntry artist in artists)
                artistList.Items.Add(artist.Name);
        }

        /// <summary>
        /// 下载前执行
        /// </summary>
        private void BeforeDownload()
        {
            // 冻结所有控件
            Freeze();

            // 修改回车焦点
            SetAcceptButton(this.ButtonCancle);

            // 下载总数和跳过总数清零
            totalDownloadNum = 0;
            skipDownloadNum = 0;
            currentDownloadTaskNum = 0;

            // 初始化开始时间
            startTime = DateTime.Now;

            // 设置最大线程数
            DownloadManager.SetMaxThreads(16);
        }

        /// <summary>
        /// 结束下载时执行
        /// </summary>
        private void AfterDownload()
        {
            // 解冻所有控件
            UnFreeze();

            DateTime now = DateTime.Now;// 下载结束后的时间
            TimeSpan time = now - startTime;// 总下载时间
            AddItemToTextBox(String.Format("共{0}小时{1}分钟{2}秒", time.Hours, time.Minutes, time.Seconds + "秒"));
            AddItemToTextBox(String.Format("下载了{0}张，跳过了{1}张\r\n", totalDownloadNum, skipDownloadNum));

            // 修改回车焦点
            SetAcceptButton(this.ButtonUpdateList);
        }

        private void SetAcceptButton(Button button)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<Button>(SetAcceptButton), button);
                }
                else
                {
                    AcceptButton = button;
                }
            }
            catch
            {
            }
        }
        #endregion
        #region 下载相关
        /// <summary>
        /// 下载勾选上的图集
        /// </summary>
        private void DoDownload()
        {
            // 检查必须的信息
            if (input_name.Text == "")
            {
                MessageBox.Show("作者名不能为空！");
                return;
            }

            BeforeDownload();

            try
            {
                if (RadioButtonGallery.Checked)
                {
                    download("gallery");
                }
                else if (RadioButtonScraps.Checked)
                {
                    download("scraps");
                }

                DownloadManager.WaitForAllFinished();
            }
            catch (Exception e)
            {
                AddItemToTextBox(e.Message);
                DownloadManager.StopAll();
                DownloadManager.WaitForAllFinished();
            }

            AfterDownload();
        }

        /// <summary>
        /// 下载图集
        /// </summary>
        /// <param name="type">gallery或者scraps</param>
        private void download(string type)
        {
            AddItemToTextBox(type + "下载开始");

            // 循环下载第pageNum页
            while (true)
            {
                try
                {
                    if (isStop)
                    {
                        throw new InterruptException("正在结束下载");
                    }

                    // 存储当前页面地址
                    string nowUrl = string.Format("https://www.furaffinity.net/{0}/{1}/{2}",
                                                    type,
                                                    userName,
                                                    startPageNum);

                    // 下载当前页
                    string html = GetHtml(nowUrl);

                    // 检查页面是否下载成功
                    if (html == null || html == "")
                        throw new NetworkException("请检查网络");

                    // 检查此用户是否存在
                    if (!Analyze.HasUser(html))
                        throw new NoSuchArtistException("未查询到该作者，请检查作者名称");

                    // 检查此页是否需要登录才能访问
                    if (!Analyze.NeedLogin(html))
                        throw new NeedLoginException("需要登陆后才能下载此作者的作品");

                    //--------开始详情页下载循环------------

                    // 获得所有详情页
                    List<String> pages = Analyze.GetPages(html);

                    // 检查此页是否还有图片，没有的话就结束并等待所有任务完成
                    AddItemToTextBox(String.Format("第{0}页共{1}张图片", startPageNum, pages.Count));
                    if (pages.Count == 0)
                        return;

                    for (int i = startPicNum - 1; i < pages.Count; ++i)
                    {
                        // 停止时不再添加新的任务
                        if (isStop)
                        {
                            throw new InterruptException("正在结束下载");
                        }

                        // 在到达最大下载量时停止
                        if (maxDownloadNum != 0 && currentDownloadTaskNum >= maxDownloadNum)
                        {
                            AddItemToTextBox("已到达最大任务量，正在等待下载完成");
                            return;
                        }

                        currentDownloadTaskNum++;
                        downloadPicture(pages[i], type);
                    }
                    startPicNum = 1;
                    startPageNum++;
                }
                catch (NetworkException)
                {
                    if (retryOnNetworkErrors)
                    {
                        AddItemToTextBox("网络不稳定，将在一秒后重试");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 根据详情页url下载此页面，解析其中的图片地址并下载
        /// </summary>
        /// <param name="pageUrl"></param>
        /// <param name="type"></param>
        private void downloadPicture(string pageUrl, string type)
        {
            // 每个详情页只请求一次，同时解析图片和描述。
            string html = GetHtml(pageUrl);
            if (String.IsNullOrEmpty(html))
                throw new Exception("请检查网络");

            SubmissionInfo submission = Analyze.GetSubmissionInfo(pageUrl, html);
            string pictureUrl = submission.PictureUrl;
            string pictureName = submission.PictureName;
            string defaultRelativePath = Path.Combine(
                userName,
                type == "gallery" ? pictureName : Path.Combine(type, pictureName));
            string indexedRelativePath;
            bool indexed;
            lock (downloadIndexLock)
            {
                indexed = downloadIndex.TryGetPath(pictureUrl, out indexedRelativePath);
            }

            string relativePath = indexed
                ? GetSafeRelativePath(indexedRelativePath, defaultRelativePath)
                : defaultRelativePath;
            string fullFilePath = Path.Combine(downloadRootDirectory, relativePath);
            bool alreadyQueued;
            lock (queuedDownloadLock)
                alreadyQueued = queuedDownloadUrls.Contains(pictureUrl);
            string descriptionPath = GetDescriptionPath(submission, indexed || alreadyQueued);
            DescriptionWriter.Write(descriptionPath, submission.Description);

            if (alreadyQueued || File.Exists(fullFilePath))
            {
                if (!indexed)
                {
                    lock (downloadIndexLock)
                        downloadIndex.Record(pictureUrl, relativePath);
                }

                AddItemToTextBox(String.Format("第{0}页第{1}张已存在，跳过并更新描述", startPageNum, startPicNum));
                startPicNum++;
                Interlocked.Increment(ref skipDownloadNum);
                return;
            }

            // 使用多线程下载
            lock (queuedDownloadLock)
                queuedDownloadUrls.Add(pictureUrl);
            string targetDirectory = Path.GetDirectoryName(fullFilePath);
            Task task = new Task()
            {
                pageNum = startPageNum,
                picNum = startPicNum,

                Url = pictureUrl,
                Cookie = cookie,

                FilePath = targetDirectory + Path.DirectorySeparatorChar,
                FileName = pictureName,
                TaskStart = new Task.TaskStartDelegate(delegate (int id, Task t)
                {
                    // 下载开始
                }),
                TaskStop = new Task.TaskStopDelegate(delegate (int id, Task t)
                {
                    // AddItemToTextBox("下载被终止" + t.FileName);
                }),
                TaskFinish = new Task.TaskFinishDelegate(delegate (int id, Task t)
                {
                    lock (downloadIndexLock)
                        downloadIndex.Record(pictureUrl, relativePath);
                    Interlocked.Increment(ref totalDownloadNum);

                    // 计算下载所用时间
                    DateTime now = DateTime.Now;
                    TimeSpan time = now - startTime;

                    AddItemToTextBox(String.Format("第{0}页第{1}张下载完成", t.pageNum, t.picNum));
                }),
                TaskFail = new Task.TaskFailDelegate(delegate (int id, Task t)
                {
                    lock (queuedDownloadLock)
                        queuedDownloadUrls.Remove(pictureUrl);
                    AddItemToTextBox(String.Format("第{0}页第{1}张下载失败，文件名：{2}，url：{3}", t.pageNum, t.picNum, t.FileName, t.Url));
                }),
            };
            DownloadManager.Add(task);
            startPicNum++;
        }

        private string GetDescriptionPath(SubmissionInfo submission, bool indexed)
        {
            string descriptionDirectory = Path.Combine(filePath, "descriptions");
            if (indexed)
            {
                string baseName = Path.GetFileNameWithoutExtension(submission.PictureName);
                return Path.Combine(descriptionDirectory, baseName + ".txt");
            }

            return DescriptionWriter.GetPath(filePath, submission.PictureName, submission.SubmissionId);
        }

        private string GetSafeRelativePath(string indexedPath, string fallbackPath)
        {
            if (String.IsNullOrWhiteSpace(indexedPath))
                return fallbackPath;

            try
            {
                string root = Path.GetFullPath(downloadRootDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string candidate = Path.GetFullPath(Path.Combine(downloadRootDirectory, indexedPath));
                if (candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return indexedPath;
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }

            return fallbackPath;
        }

        private void UpdateArtistList()
        {
            IList<string> plan = ArtistUpdatePlan.Build(artists);
            if (plan.Count == 0)
            {
                AddItemToTextBox("画师列表为空，请先添加画师");
                return;
            }

            BeforeDownload();
            lock (queuedDownloadLock)
                queuedDownloadUrls.Clear();

            try
            {
                BatchUpdateSummary summary = ArtistUpdateRunner.Run(
                    plan,
                    UpdateArtist,
                    delegate { return isStop; },
                    delegate (string artist, Exception error)
                    {
                        AddItemToTextBox(artist + " 更新失败：" + error.Message);
                    });

                lock (downloadIndexLock)
                    downloadIndex.Save();

                AddItemToTextBox(String.Format(
                    "列表更新完成：成功 {0}，失败 {1}，停止 {2}",
                    summary.Succeeded,
                    summary.Failed,
                    summary.Stopped));
            }
            catch (Exception error)
            {
                AddItemToTextBox("列表更新异常：" + error.Message);
                DownloadManager.StopAll();
                DownloadManager.WaitForAllFinished();
            }
            finally
            {
                AfterDownload();
            }
        }

        private void GetUserInputOptionsForBatch()
        {
            downloadRootDirectory = GetDownloadRootDirectory();
            ReadDownloadOptions();
            retryOnNetworkErrors = checkBoxRetry.Checked;
        }

        private void UpdateArtist(string artist)
        {
            userName = artist;
            filePath = Path.Combine(downloadRootDirectory, userName);
            startPageNum = 1;
            startPicNum = 1;
            AddItemToTextBox("开始更新画师：" + userName);

            try
            {
                download("gallery");
                if (!isStop)
                {
                    startPageNum = 1;
                    startPicNum = 1;
                    download("scraps");
                }
            }
            catch (InterruptException)
            {
                if (!isStop)
                    throw;
            }
            finally
            {
                DownloadManager.WaitForAllFinished();
                lock (downloadIndexLock)
                    downloadIndex.Save();
            }
        }

        /// <summary>
        /// 下载所给网址指向的页面信息
        /// </summary>
        /// <param name="url">网址</param>
        /// <returns>页面信息</returns>
        public string GetHtml(string url)
        {
            HttpItem item = new HttpItem()
            {
                URL = url,//URL     必需项
                Encoding = Encoding.UTF8,//编码格式（utf-8,gb2312,gbk）     可选项 默认类会自动识别
                                         //Encoding = Encoding.Default,
                Method = "get",//URL     可选项 默认为Get
                Timeout = 30000,//连接超时时间     可选项默认为100000
                IsToLower = false,//得到的HTML代码是否转成小写     可选项默认转小写
                Cookie = cookie,//字符串Cookie     可选项
                UserAgent = "PostmanRuntime/7.17.1",//用户的浏览器类型，版本，操作系统     可选项有默认值
                Accept = "*/*",//    可选项有默认值
                Referer = "https://www.furaffinity.net",//来源URL     可选项
                Host = "www.furaffinity.net",
                Allowautoredirect = true,
                MaximumAutomaticRedirections = 5,
                ResultType = ResultType.String,//返回数据类型，是Byte还是String
                Connectionlimit = 16,
                // ProxyIp = "127.0.0.1:1081",
            };

            try
            {
                var response = http.GetHtml(item);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new NetworkException("FA 请求失败，状态码：" + response.StatusCode);
                }

                // 返回html
                return response.Html;
            }
            catch (NetworkException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw new NetworkException("网络异常：" + e.Message);
            }
        }
        #endregion
    }
}
