$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wvCore = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.Core.dll"
$wvForms = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$wvLoader = Join-Path $root "deps\wv2\runtimes\win-x64\native\WebView2Loader.dll"
$appExe = Join-Path $root "dist\dejavu.exe"
$setupExe = Join-Path $root "dist\dejavu-Setup.exe"

if (!(Test-Path $appExe)) {
  & (Join-Path $root "build.ps1")
}

$args = @(
  "/nologo",
  "/target:winexe",
  "/platform:x64",
  "/optimize+",
  "/out:$setupExe",
  "/win32icon:$root\assets\dejavu.ico",
  "/win32manifest:$root\src\app.manifest",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Web.Extensions.dll",
  "/reference:Microsoft.CSharp.dll",
  "/reference:$wvCore",
  "/reference:$wvForms",
  "/resource:$wvCore,Microsoft.Web.WebView2.Core.dll",
  "/resource:$wvForms,Microsoft.Web.WebView2.WinForms.dll",
  "/resource:$wvLoader,WebView2Loader.dll",
  "/resource:$appExe,dejavu.exe",
  "/resource:$root\installer\setup.html,setup.html",
  "/resource:$root\ui\liquid-glass.js,liquid-glass.js",
  "/resource:$root\assets\logo-transparent.png,logo-transparent.png",
  "/resource:$root\assets\dejavu.ico,dejavu.ico",
  "/resource:$root\assets\wallpapers\wp-m3-butterfly-obsidian.jpg,wp-amethyst.jpg",
  "$root\installer\SetupApp.cs"
)

Write-Host "Building butterfly-themed dejavu-Setup.exe installer..."
& $csc @args
if ($LASTEXITCODE -ne 0) {
  throw "Setup compilation failed with exit code $LASTEXITCODE"
}

$fi = Get-Item $setupExe
Write-Host ("Built installer successfully: {0} ({1:N0} KB)" -f $fi.FullName, ($fi.Length / 1024))
