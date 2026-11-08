param([string]$Artist = 'matatabi88')
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$exe = Join-Path $PSScriptRoot '..\bin\Release\FurryDownloader.exe'
Add-Type -AssemblyName System.Windows.Forms
$release = Split-Path (Resolve-Path -LiteralPath $exe).Path
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Threading;
public static class SmokeAssemblyResolver {
    private static string directory;
    public static void Install(string path) {
        directory = path;
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args) {
        string path = Path.Combine(directory, new AssemblyName(args.Name).Name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
public sealed class SmokeDownloadRunner {
    private readonly object target;
    private readonly MethodInfo method;
    public volatile bool Done;
    public Exception Error;
    public SmokeDownloadRunner(object target, MethodInfo method) { this.target = target; this.method = method; }
    public void Start() { var thread = new Thread(Run); thread.IsBackground = true; thread.Start(); }
    private void Run() {
        try { method.Invoke(target, new object[] { "gallery" }); }
        catch (Exception error) { Error = error; }
        finally { Done = true; }
    }
}
'@
[SmokeAssemblyResolver]::Install($release)
foreach ($dll in @('System.Runtime.CompilerServices.Unsafe.dll', 'System.Buffers.dll', 'System.Memory.dll', 'System.Numerics.Vectors.dll', 'System.Resources.Extensions.dll', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $release $dll)) | Out-Null
}
[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $exe).Path) | Out-Null
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('FurryDownloader-smoke-' + [Guid]::NewGuid().ToString('N'))
$form = $null
try {
    [IO.Directory]::CreateDirectory($scratch) | Out-Null
    $form = New-Object FurryDownloader.mainFrom
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    function Set-Field([string]$name, $value) { $form.GetType().GetField($name, $flags).SetValue($form, $value) }
    function Get-Field([string]$name) { return $form.GetType().GetField($name, $flags).GetValue($form) }
    Set-Field 'userName' $Artist
    Set-Field 'downloadRootDirectory' $scratch
    Set-Field 'filePath' (Join-Path $scratch $Artist)
    Set-Field 'downloadIndex' ([FurryDownloader.DownloadIndex]::Load((Join-Path $scratch 'index.xml')))
    Set-Field 'concurrency' 4
    Set-Field 'maxDownloadNum' 4
    Set-Field 'retryOnNetworkErrors' $true
    $download = $form.GetType().GetMethod('download', $flags)
    [void]$form.Handle
    $logBox = $form.GetType().GetField('textBox', $flags).GetValue($form)
    [void]$logBox.Handle
    function Run-Download {
        $runner = New-Object SmokeDownloadRunner($form, $download)
        $runner.Start()
        while (-not $runner.Done) { [Windows.Forms.Application]::DoEvents(); [Threading.Thread]::Sleep(20) }
        if ($runner.Error) { throw $runner.Error.ToString() }
    }
    Run-Download
    $images = @(Get-ChildItem -LiteralPath $scratch -Recurse -File | Where-Object { $_.Extension -ne '.txt' })
    $descriptions = @(Get-ChildItem -LiteralPath $scratch -Recurse -File -Filter '*.txt')
    if ($images.Count -ne 4 -or $descriptions.Count -ne 4 -or (Get-Field 'totalDownloadNum') -ne 4) { throw 'First pass did not save four complete images and descriptions.' }
    Set-Field 'startPageNum' 1
    Set-Field 'startPicNum' 1
    Set-Field 'currentDownloadTaskNum' 0
    Run-Download
    if ((Get-Field 'skipDownloadNum') -ne 4 -or (Get-Field 'totalDownloadNum') -ne 4) { throw 'Second pass did not skip all four existing images.' }
    Write-Output 'Live smoke passed: four images and descriptions saved; second pass skipped all four.'
} finally {
    if ($form) { $form.Dispose() }
    if (Test-Path -LiteralPath $scratch) { [IO.Directory]::Delete($scratch, $true) }
}
