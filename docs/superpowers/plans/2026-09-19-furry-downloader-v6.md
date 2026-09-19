# FurryDownloader v6 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 FurryDownloader 升级为可维护画师列表、一次更新列表中所有画师最新作品、持久化去重、保存作品描述并可靠获取 FA 登录 Cookie 的 WinForms 应用。

**Architecture:** 保留现有下载线程池和 FA 分页流程，把画师列表、作品解析、下载索引、描述写入、Cookie 格式化拆成可测试的纯逻辑组件；主窗体只负责列表编辑、批量调度和日志。登录窗口改用 WebView2，下载详情页与图片请求统一使用同一份 Cookie。

**Tech Stack:** C# WinForms, .NET Framework 4.8, Microsoft.Web.WebView2, System.Xml.Linq, xUnit test project targeting net8.0 for pure logic components.

**Spec:** `docs/superpowers/specs/2026-09-19-furry-downloader-v6-design.md`

## Global Constraints

- 所有实现只提交到 `codex/furry-downloader-v6`，保持 `master` 不变。
- 用户操作只有一个批量更新入口：“更新列表”；内部可以逐个处理画师，但不提供单画师更新主流程。
- 点击“更新列表”按 XML 中的顺序串行处理所有画师，每个画师都处理 `gallery` 和 `scraps`。
- 画师列表保存到 `我的文档\FaDownloader\artists.xml`，下载索引保存到 `我的文档\FaDownloader\download-index.xml`。
- 描述保存到 `<根目录>\<画师>\descriptions\`，编码为 UTF-8；图片目录布局保持现有兼容行为。
- 不把完整 Cookie 写入日志；没有 WebView2 Runtime 时必须显示可操作的错误提示。
- 所有新增纯逻辑行为先写会失败的测试，再写最小生产实现；每个任务结束运行测试并独立提交。
- 不引入图片哈希、代理池、账号池或并行画师调度。

## Review Focus

- 画师列表为空、包含空白名、重复名或名称前后带空格时，更新入口必须给出明确结果且不发起错误请求；由 Task 2 的列表测试覆盖。
- FA 详情页缺少描述、描述含嵌套标签/实体/`<br>` 或图片链接为协议相对 URL 时，图片仍能下载且文本正确；由 Task 3 的解析测试覆盖。
- 下载索引损坏、索引缺失但本地文件已存在、同一 URL 在两个分类中重复出现时，不能误删图片或重复排队；由 Task 4 的索引测试覆盖。
- Cookie 顺序变化、只有 `a` Cookie、Cookie 为空以及 WebView2 Runtime 缺失时，登录状态必须正确识别或给出明确错误；由 Task 5 的格式化测试和人工验收覆盖。
- 列表中间一个画师失败时，后续画师仍要处理，停止操作不能留下未完成任务；由 Task 6 的批量计划测试和人工验收覆盖。

## File Map

- Modify: `FurryDownloader.csproj` — 升级目标框架、启用旧资源生成兼容选项、加入 WebView2 和资源扩展包引用。
- Create: `tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj` — 纯逻辑 xUnit 测试项目。
- Create: `tests/FurryDownloader.Tests/*Tests.cs` — 列表、解析、索引、Cookie、批量计划测试。
- Create: `src/ArtistEntry.cs` — 画师条目模型与名称规范化。
- Create: `src/ArtistListStore.cs` — 画师 XML 的加载、原子保存和损坏恢复。
- Create: `src/SubmissionInfo.cs` — 作品详情解析结果模型。
- Modify: `src/Analyze.cs` — 解析图片 URL、作品 ID 和描述纯文本。
- Create: `src/DownloadIndex.cs` — 图片 URL 到相对路径的持久化索引。
- Create: `src/DescriptionWriter.cs` — 安全生成和写入描述文件路径。
- Create: `src/CookieFormatter.cs` — WebView2 Cookie 转请求头和身份 Cookie 判断。
- Create: `src/DownloadRequestData.cs` — 为图片下载任务保存 URL 与 Cookie 的纯逻辑请求数据。
- Modify: `src/DownloadManager.cs` — 任务携带 Cookie，并把请求数据映射到现有 `HttpItem`。
- Modify: `FaLogin.cs` and `FaLogin.Designer.cs` — WebView2 登录、CookieManager 获取和 Runtime 错误提示。
- Modify: `FaDownloader.cs` and `FaDownloader.Designer.cs` — 画师列表、更新列表按钮、批量调度和日志。
- Modify: `README.md` — 新的使用方式、数据目录和 WebView2 Runtime 要求。
- Delete from project compilation: `src/FullWebBrowserCookie.cs` — 不再使用 WinInet/IE Cookie 读取实现；文件本身可在最后清理提交中删除。

---

### Task 1: Modernize the legacy build and add the test project

**Files:**
- Modify: `FurryDownloader.csproj:1-170`
- Create: `tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj`
- Create: `tests/FurryDownloader.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: a Release build targetable by `dotnet msbuild` and a runnable xUnit project at `tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj`.

- [ ] **Step 1: Record the failing legacy build**

Run:

```powershell
dotnet msbuild FurryDownloader.csproj /t:Build /p:Configuration=Release /nologo
```

Expected before this task: failure mentioning `GenerateResourceUsePreserializedResources` and `System.Resources.Extensions`.

- [ ] **Step 2: Add the framework and resource build settings**

Modify the main project so the first property group contains:

```xml
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
<GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>
```

Add these package references before the existing `Microsoft.CSharp.targets` import:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.3485.44" />
  <PackageReference Include="System.Resources.Extensions" Version="8.0.0" />
</ItemGroup>
```

- [ ] **Step 3: Create the runnable test project**

Create `tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

Create the initial smoke test:

```csharp
using Xunit;

namespace FurryDownloader.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void TestRunnerIsAvailable()
        {
            Assert.Equal(4, 2 + 2);
        }
    }
}
```

- [ ] **Step 4: Verify build and test infrastructure**

Run:

```powershell
dotnet restore tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --no-restore
dotnet msbuild FurryDownloader.csproj /t:Build /p:Configuration=Release /nologo
```

Expected: the smoke test passes and the WinForms project exits with code 0.

- [ ] **Step 5: Commit the build foundation**

```powershell
git add FurryDownloader.csproj tests/FurryDownloader.Tests
git commit -m "build: modernize legacy project and add test harness"
```

### Task 2: Add persistent artist-list storage and list-level update plan

**Files:**
- Create: `src/ArtistEntry.cs`
- Create: `src/ArtistListStore.cs`
- Create: `src/ArtistUpdatePlan.cs`
- Modify: `FurryDownloader.csproj` to compile the three new files
- Modify: `tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj` to link the three source files
- Create: `tests/FurryDownloader.Tests/ArtistListTests.cs`

**Interfaces:**
- Produces `ArtistName.Normalize(string input) : string`.
- Produces `ArtistListStore.Load() : IList<ArtistEntry>` and `ArtistListStore.Save(IEnumerable<ArtistEntry> artists) : void`.
- Produces `ArtistUpdatePlan.Build(IEnumerable<ArtistEntry> artists) : IList<string>`; it preserves XML order and removes case-insensitive duplicates.

- [ ] **Step 1: Write failing list and plan tests**

Add tests with the following behaviors:

```csharp
[Fact]
public void SaveThenLoadPreservesOrder()
{
    var path = Path.Combine(Path.GetTempPath(), "furry-artists-" + Guid.NewGuid() + ".xml");
    try
    {
        var store = new ArtistListStore(path);
        store.Save(new[] { new ArtistEntry("Alpha"), new ArtistEntry("Beta") });

        var loaded = store.Load();

        Assert.Equal(new[] { "Alpha", "Beta" }, loaded.Select(x => x.Name));
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

[Fact]
public void BuildRemovesCaseInsensitiveDuplicatesAndKeepsFirstOrder()
{
    var plan = ArtistUpdatePlan.Build(new[] {
        new ArtistEntry("Alpha"), new ArtistEntry("alpha"), new ArtistEntry("Beta")
    });

    Assert.Equal(new[] { "Alpha", "Beta" }, plan);
}

[Fact]
public void BuildReturnsAnEmptyPlanForAnEmptyList()
{
    Assert.Empty(ArtistUpdatePlan.Build(Array.Empty<ArtistEntry>()));
}

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void NormalizeRejectsBlankArtistNames(string input)
{
    Assert.Throws<ArgumentException>(() => ArtistName.Normalize(input));
}
```

- [ ] **Step 2: Link the not-yet-existing source files and run the tests**

Add these links to the test project:

```xml
<ItemGroup>
  <Compile Include="..\..\src\ArtistEntry.cs" Link="src\ArtistEntry.cs" />
  <Compile Include="..\..\src\ArtistListStore.cs" Link="src\ArtistListStore.cs" />
  <Compile Include="..\..\src\ArtistUpdatePlan.cs" Link="src\ArtistUpdatePlan.cs" />
</ItemGroup>
```

Run:

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~ArtistListTests"
```

Expected: FAIL because `ArtistEntry`, `ArtistListStore`, `ArtistUpdatePlan`, and `ArtistName` do not yet exist.

- [ ] **Step 3: Implement the minimal list components**

Implement the public surface exactly as follows:

```csharp
public sealed class ArtistEntry
{
    public ArtistEntry(string name) { Name = ArtistName.Normalize(name); }
    public string Name { get; private set; }
}

public static class ArtistName
{
    public static string Normalize(string input)
    {
        var value = (input ?? String.Empty).Trim();
        if (value.Length == 0) throw new ArgumentException("Artist name is required", "input");
        return value;
    }
}

public sealed class ArtistListStore
{
    public ArtistListStore(string path) { }
    public IList<ArtistEntry> Load() { }
    public void Save(IEnumerable<ArtistEntry> artists) { }
}

public static class ArtistUpdatePlan
{
    public static IList<string> Build(IEnumerable<ArtistEntry> artists) { }
}
```

Use `XDocument` with `<artists><artist>name</artist></artists>`, save through `path + ".tmp"`, and replace the destination only after the temporary document is written. A missing or malformed file returns an empty list without deleting the original file.

- [ ] **Step 4: Run the focused and full test suites**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~ArtistListTests"
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 5: Commit the list foundation**

```powershell
git add src/ArtistEntry.cs src/ArtistListStore.cs src/ArtistUpdatePlan.cs FurryDownloader.csproj tests/FurryDownloader.Tests
git commit -m "feat: add persistent artist list and batch update plan"
```

### Task 3: Parse works and write descriptions

**Files:**
- Create: `src/SubmissionInfo.cs`
- Modify: `src/Analyze.cs`
- Create: `src/DescriptionWriter.cs`
- Modify: `FurryDownloader.csproj` and test project links
- Create: `tests/FurryDownloader.Tests/AnalyzeTests.cs`
- Create: `tests/FurryDownloader.Tests/DescriptionWriterTests.cs`

**Interfaces:**
- Produces `SubmissionInfo` with `PageUrl`, `SubmissionId`, `PictureUrl`, `PictureName`, and `Description` properties.
- Produces `Analyze.GetSubmissionInfo(string pageUrl, string html) : SubmissionInfo`.
- Produces `DescriptionWriter.GetPath(string artistDirectory, string imageName, string submissionId) : string` and `DescriptionWriter.Write(string path, string description) : void`.

- [ ] **Step 1: Add failing parser tests from a fixed HTML fixture**

Use a fixture containing a protocol-relative download URL and description markup:

```csharp
private const string DetailHtml = @"
<a href=""//d.facdn.net/art/artist/1/work.png"">Download</a>
<div class=""submission-description-text user-submitted-links"">
  Hello &amp; welcome<br />
  <strong>Second line</strong>
</div>";

[Fact]
public void GetSubmissionInfoExtractsImageAndPlainTextDescription()
{
    var info = Analyze.GetSubmissionInfo("https://www.furaffinity.net/view/123/", DetailHtml);

    Assert.Equal("https://d.facdn.net/art/artist/1/work.png", info.PictureUrl);
    Assert.Equal("work.png", info.PictureName);
    Assert.Contains("Hello & welcome", info.Description);
    Assert.Contains("Second line", info.Description);
}
```

Also test missing description returns `string.Empty`, and an HTTP download URL remains unchanged.

- [ ] **Step 2: Link the parser sources and run the tests to verify the expected failure**

Add these links to the test project:

```xml
<ItemGroup>
  <Compile Include="..\..\src\Analyze.cs" Link="src\Analyze.cs" />
  <Compile Include="..\..\src\SubmissionInfo.cs" Link="src\SubmissionInfo.cs" />
  <Compile Include="..\..\src\DescriptionWriter.cs" Link="src\DescriptionWriter.cs" />
</ItemGroup>
```

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~AnalyzeTests"
```

Expected: FAIL because `SubmissionInfo` and `GetSubmissionInfo` do not yet exist.

- [ ] **Step 3: Implement the parser and description path writer**

Use a case-insensitive regex for the first anchor whose text follows the `Download` link, normalize `//` to `https:`, extract the numeric view ID from the page URL, and convert the `submission-description-text` fragment by applying these operations in order:

```csharp
fragment = Regex.Replace(fragment, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
fragment = Regex.Replace(fragment, @"<[^>]+>", String.Empty, RegexOptions.Singleline);
fragment = WebUtility.HtmlDecode(fragment);
fragment = fragment.Replace("\r\n", "\n").Replace("\r", "\n");
description = Regex.Replace(fragment, @"[ \t]+\n", "\n").Trim();
```

`DescriptionWriter.GetPath` must use only the image base name, remove path separators, and append the submission ID when a collision would otherwise occur. `Write` must create the parent directory and use `new UTF8Encoding(false)`.

- [ ] **Step 4: Run parser and writer tests, then the full suite**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~AnalyzeTests|FullyQualifiedName~DescriptionWriterTests"
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
```

Expected: all tests pass with no warnings from the test runner.

- [ ] **Step 5: Commit parsing and descriptions**

```powershell
git add src/SubmissionInfo.cs src/Analyze.cs src/DescriptionWriter.cs FurryDownloader.csproj tests/FurryDownloader.Tests
git commit -m "feat: parse submission descriptions and save text files"
```

### Task 4: Add persistent URL deduplication and authenticated image requests

**Files:**
- Create: `src/DownloadIndex.cs`
- Create: `src/DownloadRequestData.cs`
- Modify: `src/DownloadManager.cs`
- Modify: `FurryDownloader.csproj` and test project links
- Create: `tests/FurryDownloader.Tests/DownloadIndexTests.cs`
- Create: `tests/FurryDownloader.Tests/DownloadRequestDataTests.cs`

**Interfaces:**
- Produces `DownloadIndex.Load(string path) : DownloadIndex`, `TryGetPath(string url, out string relativePath) : bool`, `Record(string url, string relativePath) : void`, and `Save() : void`.
- Produces `DownloadRequestData(string url, string cookie)` with read-only `Url` and `Cookie` properties.
- Extends `FurryDownloader.Task` with `Cookie` and uses that value when creating the image `HttpItem`.

- [ ] **Step 1: Write failing index and Cookie propagation tests**

```csharp
[Fact]
public void RecordedUrlCanBeLoadedAndResolvedToRelativePath()
{
    var path = Path.Combine(Path.GetTempPath(), "furry-index-" + Guid.NewGuid() + ".xml");
    try
    {
        var index = DownloadIndex.Load(path);
        index.Record("https://d.facdn.net/art/a.png", "Artist\\a.png");
        index.Save();

        var reloaded = DownloadIndex.Load(path);
        Assert.True(reloaded.TryGetPath("https://d.facdn.net/art/a.png", out var relative));
        Assert.Equal("Artist\\a.png", relative);
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

```

Add tests for URL normalization, malformed index recovery, and duplicate `Record` calls keeping one mapping.

Create `DownloadRequestDataTests.cs` with:

```csharp
[Fact]
public void DownloadRequestDataKeepsTheLoginCookie()
{
    var request = new DownloadRequestData(
        "https://d.facdn.net/art/a.png", "a=identity; b=session");

    Assert.Equal("https://d.facdn.net/art/a.png", request.Url);
    Assert.Equal("a=identity; b=session", request.Cookie);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail for missing behavior**

Add these links to the test project:

```xml
<ItemGroup>
  <Compile Include="..\..\src\DownloadIndex.cs" Link="src\DownloadIndex.cs" />
  <Compile Include="..\..\src\DownloadRequestData.cs" Link="src\DownloadRequestData.cs" />
</ItemGroup>
```

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~DownloadIndexTests|FullyQualifiedName~DownloadRequestDataTests"
```

Expected: FAIL because the index and request data type do not yet exist.

- [ ] **Step 3: Implement the index and request data contract**

Store entries as `<downloads><item url="..." path="..." /></downloads>`, normalize URLs by trimming whitespace and converting an initial `//` to `https://`, and write through a temporary file. `Load` must return an empty index if the file is missing; if parsing fails, copy the original XML to `path + ".bak"`, return an empty index, and never delete downloaded files.

Implement `DownloadRequestData` as an immutable two-property contract, then use it inside `TaskWorker.DownloadAndSave`. Build the `HttpItem` with the same headers currently used by `DownloadManager`, plus:

```csharp
Cookie = requestData.Cookie,
Referer = "https://www.furaffinity.net",
ResultType = ResultType.Byte
```

Modify `TaskWorker.DownloadAndSave` to construct `new DownloadRequestData(task.Url, task.Cookie)`, copy its `Url` and `Cookie` into the `HttpItem`, and keep the existing file-exists guard.

- [ ] **Step 4: Run focused tests and full tests**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~DownloadIndexTests|FullyQualifiedName~DownloadRequestDataTests"
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 5: Commit deduplication and Cookie propagation**

```powershell
git add src/DownloadIndex.cs src/DownloadRequestData.cs src/DownloadManager.cs FurryDownloader.csproj tests/FurryDownloader.Tests
git commit -m "feat: persist download index and propagate auth cookies"
```

### Task 5: Replace IE login with WebView2 CookieManager

**Files:**
- Create: `src/FaCookie.cs`
- Create: `src/CookieFormatter.cs`
- Modify: `FaLogin.cs`
- Modify: `FaLogin.Designer.cs`
- Modify: `FurryDownloader.csproj`
- Create: `tests/FurryDownloader.Tests/CookieFormatterTests.cs`
- Delete: `src/FullWebBrowserCookie.cs`

**Interfaces:**
- Produces `FaCookie(string name, string value)` with read-only `Name` and `Value`.
- Produces `CookieFormatter.HasIdentityCookie(IEnumerable<FaCookie> cookies) : bool` and `CookieFormatter.ToRequestHeader(IEnumerable<FaCookie> cookies) : string`.
- `FaLogin.Cookie` remains the existing public string property so `FaDownloader` does not need a login API change.

- [ ] **Step 1: Write failing CookieFormatter tests**

```csharp
[Fact]
public void IdentityCookieIsDetectedRegardlessOfCookieOrder()
{
    var cookies = new[] {
        new FaCookie("b", "session"),
        new FaCookie("a", "identity")
    };

    Assert.True(CookieFormatter.HasIdentityCookie(cookies));
    Assert.Equal("b=session; a=identity", CookieFormatter.ToRequestHeader(cookies));
}

[Fact]
public void EmptyIdentityCookieDoesNotCountAsLoggedIn()
{
    Assert.False(CookieFormatter.HasIdentityCookie(new[] { new FaCookie("a", "") }));
}
```

- [ ] **Step 2: Run the tests and confirm the expected failure**

Add these links to the test project:

```xml
<ItemGroup>
  <Compile Include="..\..\src\FaCookie.cs" Link="src\FaCookie.cs" />
  <Compile Include="..\..\src\CookieFormatter.cs" Link="src\CookieFormatter.cs" />
</ItemGroup>
```

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~CookieFormatterTests"
```

Expected: FAIL because `FaCookie` and `CookieFormatter` do not yet exist.

- [ ] **Step 3: Implement the pure CookieFormatter layer**

Ignore null/empty cookie names, preserve the WebView2 enumeration order in the request header, and only treat a non-empty `a` cookie as authenticated. Do not write Cookie values to test output or application logs.

- [ ] **Step 4: Replace the login control**

Replace the designer `WebBrowser` field with `Microsoft.Web.WebView2.WinForms.WebView2`. On `Shown`, initialize it with the user data folder `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "FaDownloader", "WebView2")`. After `CoreWebView2InitializationCompleted`, navigate to `https://www.furaffinity.net/login/?mode=imagecaptcha`.

On `NavigationCompleted`, retrieve cookies with:

```csharp
var cookies = await WebView.CoreWebView2.CookieManager
    .GetCookiesAsync("https://www.furaffinity.net/");
var faCookies = cookies.Select(c => new FaCookie(c.Name, c.Value)).ToList();
if (CookieFormatter.HasIdentityCookie(faCookies))
{
    Cookie = CookieFormatter.ToRequestHeader(faCookies);
    Close();
}
```

Guard the initialization with `try/catch` for `WebView2RuntimeNotFoundException` and general initialization failures; show a message that includes the Microsoft Edge WebView2 Runtime requirement. Do not close the form merely because a navigation completed without the `a` Cookie.

- [ ] **Step 5: Run tests and build the login code**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
dotnet msbuild FurryDownloader.csproj /t:Build /p:Configuration=Release /nologo
```

Expected: all pure logic tests pass and the WinForms project builds.

- [ ] **Step 6: Commit the WebView2 login implementation**

```powershell
git add src/FaCookie.cs src/CookieFormatter.cs FaLogin.cs FaLogin.Designer.cs FurryDownloader.csproj tests/FurryDownloader.Tests
git rm src/FullWebBrowserCookie.cs
git commit -m "fix: use WebView2 to obtain FA login cookies"
```

### Task 6: Wire list-wide update orchestration into the main form

**Files:**
- Modify: `FaDownloader.cs`
- Modify: `FaDownloader.Designer.cs`
- Modify: `src/DownloadManager.cs` to pass the batch Cookie into every image task
- Create: `src/ArtistUpdateRunner.cs`
- Modify: `FurryDownloader.csproj`
- Create: `tests/FurryDownloader.Tests/BatchUpdateRunnerTests.cs`

**Interfaces:**
- `mainFrom` loads `ArtistListStore` at startup and uses `ArtistUpdatePlan.Build` when “更新列表” is clicked.
- The batch method processes each plan name in order, always calls the existing gallery/scraps downloader for both types, and reports per-artist failures without aborting the remaining list.
- `ArtistUpdateRunner.Run(IEnumerable<string> artists, Action<string> updateArtist, Func<bool> stopRequested, Action<string, Exception> reportFailure) : BatchUpdateSummary` invokes artists in order, continues after an exception, and stops before the next artist when requested.

- [ ] **Step 1: Write the failing batch runner tests**

```csharp
[Fact]
public void RunnerContinuesAfterOneArtistFails()
{
    var processed = new List<string>();
    var result = ArtistUpdateRunner.Run(
        new[] { "first", "bad", "second" },
        artist => { processed.Add(artist); if (artist == "bad") throw new InvalidOperationException("boom"); },
        () => false,
        (artist, error) => { });

    Assert.Equal(new[] { "first", "bad", "second" }, processed);
    Assert.Equal(2, result.Succeeded);
    Assert.Equal(1, result.Failed);
}

[Fact]
public void RunnerStopsBeforeTheNextArtistWhenRequested()
{
    var processed = new List<string>();
    var result = ArtistUpdateRunner.Run(
        new[] { "first", "second" },
        artist => processed.Add(artist),
        () => processed.Count == 1,
        (artist, error) => { });

    Assert.Single(processed);
    Assert.Equal(1, result.Stopped);
}
```

- [ ] **Step 2: Link the runner source and verify the expected failure**

Add this link to the test project:

```xml
<ItemGroup>
  <Compile Include="..\..\src\ArtistUpdateRunner.cs" Link="src\ArtistUpdateRunner.cs" />
</ItemGroup>
```

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj --filter "FullyQualifiedName~BatchUpdateRunnerTests"
```

Expected: FAIL because `ArtistUpdateRunner` and `BatchUpdateSummary` do not yet exist.

- [ ] **Step 3: Implement the batch runner**

Implement `BatchUpdateSummary` with `Succeeded`, `Failed`, and `Stopped` integer properties. `ArtistUpdateRunner.Run` must check `stopRequested()` before each new artist, invoke `updateArtist` inside `try/catch`, increment the corresponding counter, call `reportFailure` for exceptions, and continue to the next name after a failure.

- [ ] **Step 4: Add list controls and persistence**

Add a `ListBox` and buttons named `ButtonAddArtist`, `ButtonRemoveArtist`, and `ButtonUpdateList`. Keep the current artist textbox as the add input. At form construction:

```csharp
artistStore = new ArtistListStore(Path.Combine(homeDir, "artists.xml"));
artists = artistStore.Load();
RefreshArtistList();
```

The add handler constructs `new ArtistEntry(input_name.Text)`, rejects case-insensitive duplicates, saves immediately, and refreshes the list. The delete handler removes only the selected row and saves immediately. The update button reads the stored list, not an unsaved textbox value.

- [ ] **Step 5: Refactor the download loop into one list update operation**

Create the batch entry point with this shape:

```csharp
private void UpdateArtistList()
{
    var plan = ArtistUpdatePlan.Build(artists);
    if (plan.Count == 0) { AddItemToTextBox("画师列表为空"); return; }

    var summary = ArtistUpdateRunner.Run(
        plan,
        UpdateArtist,
        () => isStop,
        (artist, error) => AddItemToTextBox(artist + " 更新失败：" + error.Message));
    AddItemToTextBox(String.Format("列表更新完成：成功 {0}，失败 {1}，停止 {2}",
        summary.Succeeded, summary.Failed, summary.Stopped));
}
```

`UpdateArtist` resets page and picture counters, sets the artist output directory, calls `download("gallery")`, then `download("scraps")`, waits for `DownloadManager.WaitForAllFinished()`, and passes the batch Cookie into every detail and image task. It must write the parsed description regardless of whether the image was skipped due to the index.

Change `ButtonStart_Click` to start `UpdateArtistList` on the existing worker thread, rename its visible text to `更新列表`, and freeze the list edit/update buttons while running. The existing stop handler remains the only active control during a run.

- [ ] **Step 6: Add index and description writes to the existing detail-page path**

Inside the existing `downloadPicture` flow, replace separate picture parsing with:

```csharp
SubmissionInfo submission = Analyze.GetSubmissionInfo(pageUrl, html);
string descriptionPath = DescriptionWriter.GetPath(artistDirectory, submission.PictureName, submission.SubmissionId);
DescriptionWriter.Write(descriptionPath, submission.Description);
```

Before enqueuing a picture, consult `DownloadIndex` and an in-memory `HashSet<string>` for the current run. After a successful `DownloadManager` callback, call `index.Record(submission.PictureUrl, relativePath)` and save the index once after each artist to avoid concurrent XML writes.

- [ ] **Step 7: Run the full automated suite and build**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
dotnet msbuild FurryDownloader.csproj /t:Build /p:Configuration=Release /nologo
```

Expected: all tests pass and the application builds.

- [ ] **Step 8: Commit the list-wide workflow**

```powershell
git add FaDownloader.cs FaDownloader.Designer.cs src/DownloadManager.cs src/ArtistUpdateRunner.cs src/DownloadIndex.cs src/DescriptionWriter.cs tests/FurryDownloader.Tests FurryDownloader.csproj
git commit -m "feat: update all artists from the saved list"
```

### Task 7: Documentation, manual acceptance, and final verification

**Files:**
- Modify: `README.md`
- Create: `docs/superpowers/plans/2026-09-19-furry-downloader-v6-manual-acceptance.md`

**Interfaces:**
- Documents the list-wide update behavior, storage paths, login requirement, WebView2 Runtime requirement, and recovery behavior.

- [ ] **Step 1: Update the README usage section**

Document this exact user flow:

```text
1. 选择图片根目录。
2. 输入画师名并点击“添加”，重复画师不会重复加入列表。
3. 重复添加需要维护的画师。
4. 登录 FA。
5. 点击“更新列表”，程序按列表顺序更新所有画师的 gallery 和 scraps。
6. 图片保存在画师目录，描述保存在 descriptions 子目录。
```

Document `artists.xml`, `download-index.xml`, `cookie.txt`, and the WebView2 user-data directory under `我的文档\FaDownloader`.

- [ ] **Step 2: Create the manual acceptance checklist**

Include checks for:

```text
- 首次启动列表为空，添加两个画师后重启仍保留两个画师。
- 点击一次“更新列表”会依次处理两个画师，而不是只处理当前输入框内容。
- 第二次更新不会重新下载已存在的图片，但会保留/刷新描述文本。
- gallery 和 scraps 均会处理，两个画师互不覆盖目录。
- 登录窗口可以完成 FA 登录，成功后主窗体不会再次提示登录。
- 关闭 WebView2 Runtime 后启动登录会显示明确安装提示。
- 列表中第一个画师不存在时，第二个画师仍继续更新。
- 停止后不再加入新的详情任务，已有图片任务最终进入完成状态。
```

- [ ] **Step 3: Run final automated verification**

```powershell
dotnet test tests/FurryDownloader.Tests/FurryDownloader.Tests.csproj
dotnet msbuild FurryDownloader.csproj /t:Build /p:Configuration=Release /nologo
git diff --check master...HEAD
git status --short --branch
```

Expected: all tests pass, Release build exits 0, diff check is empty, and only intended files are committed on `codex/furry-downloader-v6`.

- [ ] **Step 4: Commit documentation and verification checklist**

```powershell
git add README.md docs/superpowers/plans/2026-09-19-furry-downloader-v6-manual-acceptance.md
git commit -m "docs: document list updates and login requirements"
```

- [ ] **Step 5: Report the branch and commit history**

Run:

```powershell
git log --oneline --decorate master..HEAD
git status --short --branch
```

Report the final branch name, commit list, automated test result, build result, and any manual checks that require the user’s FA account or WebView2 Runtime.
