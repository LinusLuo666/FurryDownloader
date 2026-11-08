$ErrorActionPreference = 'Stop'
$release = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\bin\Release')).Path
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
public static class LayoutAssemblyResolver {
    private static string directory;
    public static void Install(string path) { directory = path; AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
    private static Assembly Resolve(object sender, ResolveEventArgs args) {
        string path = Path.Combine(directory, new AssemblyName(args.Name).Name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
'@
[LayoutAssemblyResolver]::Install($release)
[Reflection.Assembly]::LoadFrom((Join-Path $release 'FurryDownloader.exe')) | Out-Null
$form = New-Object FurryDownloader.mainFrom
try {
    [void]$form.Handle
    $form.PerformLayout()
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    $panel = $form.GetType().GetField('panel', $flags).GetValue($form)
    $spinner = $form.GetType().GetField('concurrencyInput', $flags).GetValue($form)
    $labelText = -join @([char]0x5e76, [char]0x53d1, [char]0x6570, [char]0xff1a)
    $label = @($panel.Controls | Where-Object { $_ -is [Windows.Forms.Label] -and $_.Text -eq $labelText })[0]
    $cache = $form.GetType().GetField('checkBoxNoCache', $flags).GetValue($form)
    $retry = $form.GetType().GetField('checkBoxRetry', $flags).GetValue($form)
    function Assert-Layout {
        if ($null -eq $label -or -not $panel.ClientRectangle.Contains($label.Bounds) -or -not $panel.ClientRectangle.Contains($spinner.Bounds)) { throw "Concurrency controls are outside panel: label=$($label.Bounds), spinner=$($spinner.Bounds), panel=$($panel.ClientRectangle)" }
        if ($label.Bounds.IntersectsWith($spinner.Bounds) -or $label.Bounds.IntersectsWith($cache.Bounds) -or $spinner.Bounds.IntersectsWith($cache.Bounds) -or $label.Bounds.IntersectsWith($retry.Bounds) -or $spinner.Bounds.IntersectsWith($retry.Bounds)) { throw 'Concurrency controls overlap.' }
        if ($spinner.Minimum -ne 1 -or $spinner.Maximum -ne 16) { throw 'Incorrect concurrency range.' }
    }
    Assert-Layout
    foreach ($factor in @(1.25, 1.2)) {
        $form.Scale([System.Drawing.SizeF]::new($factor, $factor))
        $form.PerformLayout()
        Assert-Layout
    }
    Write-Output 'Layout passed at the current scale, 125% and 150%.'
} finally { $form.Dispose() }
