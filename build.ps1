$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wvCore = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.Core.dll"
$wvForms = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$wvLoader = Join-Path $root "deps\wv2\runtimes\win-x64\native\WebView2Loader.dll"
$outExe = Join-Path $root "dist\dejavu.exe"

if (!(Test-Path $wvCore) -or !(Test-Path $wvLoader)) {
  Write-Host "Downloading official Microsoft.Web.WebView2 SDK..."
  New-Item -ItemType Directory -Force -Path (Join-Path $root "deps") | Out-Null
  $nupkg = Join-Path $root "deps\webview2.nupkg.zip"
  Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2" -OutFile $nupkg -UseBasicParsing
  Expand-Archive -Path $nupkg -DestinationPath (Join-Path $root "deps\wv2") -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $root "dist") | Out-Null

$args = @(
  "/nologo",
  "/target:winexe",
  "/platform:x64",
  "/optimize+",
  "/out:$outExe",
  "/win32icon:$root\assets\dejavu.ico",
  "/win32manifest:$root\src\app.manifest",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Web.Extensions.dll",
  "/reference:$wvCore",
  "/reference:$wvForms",
  "/resource:$wvCore,Microsoft.Web.WebView2.Core.dll",
  "/resource:$wvForms,Microsoft.Web.WebView2.WinForms.dll",
  "/resource:$wvLoader,WebView2Loader.dll",
  "/resource:$root\ui\index.html,index.html",
  "/resource:$root\ui\overlay.html,overlay.html",
  "/resource:$root\ui\styles.css,styles.css",
  "/resource:$root\ui\liquid-glass.js,liquid-glass.js",
  "/resource:$root\ui\newtab.js,newtab.js",
  "/resource:$root\ui\avatars.js,avatars.js",
  "/resource:$root\ui\app.js,app.js",
  "/resource:$root\assets\logo-transparent.png,logo-transparent.png",
  "/resource:$root\assets\dejavu-icon.png,dejavu-icon.png",
  "/resource:$root\assets\dejavu.ico,dejavu.ico",
  "/resource:$root\assets\wallpapers\wp-amethyst.jpg,wp-amethyst.jpg",
  "/resource:$root\assets\wallpapers\wp-morpho.jpg,wp-morpho.jpg",
  "/resource:$root\assets\wallpapers\wp-monarch.jpg,wp-monarch.jpg",
  "/resource:$root\assets\wallpapers\wp-sakura.jpg,wp-sakura.jpg",
  "$root\src\Program.cs",
  "$root\src\NativeWin32.cs",
  "$root\src\SettingsStore.cs",
  "$root\src\ProfileManager.cs",
  "$root\src\AutoUpdater.cs",
  "$root\src\LocalProxyBridge.cs",
  "$root\src\VpnManager.cs",
  "$root\src\SuggestEngine.cs",
  "$root\src\AdBlocker.cs",
  "$root\src\OverlayPopup.cs",
  "$root\src\BrowserForm.cs"
)

Write-Host "Building standalone native dejavu.exe..."
& $csc @args
if ($LASTEXITCODE -ne 0) {
  throw "csc.exe failed with exit code $LASTEXITCODE"
}

$fi = Get-Item $outExe
Write-Host ("Built successfully: {0} ({1:N0} KB)" -f $fi.FullName, ($fi.Length / 1024))
