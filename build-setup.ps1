$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fwDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$fwCsc = Join-Path $fwDir "csc.exe"
$csc = $fwCsc
$roslynCandidates = @(
  "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe",
  "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe",
  "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
  "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
)
foreach ($rc in $roslynCandidates) {
  if (Test-Path $rc) {
    $csc = $rc
    break
  }
}

$wvCore = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.Core.dll"
$wvForms = Join-Path $root "deps\wv2\lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$wvLoader = Join-Path $root "deps\wv2\runtimes\win-x64\native\WebView2Loader.dll"
$appExe = Join-Path $root "dist\dejavu.exe"
$setupExe = Join-Path $root "dist\dejavu-Setup.exe"

if (!(Test-Path $appExe)) {
  & (Join-Path $root "build.ps1")
}

$baseArgs = @(
  "/nologo",
  "/target:winexe",
  "/platform:x64",
  "/optimize+",
  "/highentropyva+",
  "/out:$setupExe",
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
  "/resource:$appExe,dejavu.exe",
  "/resource:$root\installer\setup.html,setup.html",
  "/resource:$root\ui\liquid-glass.js,liquid-glass.js",
  "/resource:$root\assets\logo-transparent.png,logo-transparent.png",
  "/resource:$root\assets\dejavu.ico,dejavu.ico",
  "/resource:$root\assets\wallpapers\wp-m3-butterfly-obsidian.jpg,wp-amethyst.jpg",
  "$root\installer\SetupAssemblyInfo.cs",
  "$root\src\DefaultBrowserRegistrar.cs",
  "$root\installer\SetupApp.cs"
)

$supportsDeterministic = [bool]((& $csc /? 2>&1) -match "/deterministic")
$cscArgs = if ($supportsDeterministic) {
  @("/lib:$fwDir", "/deterministic") + $baseArgs
} else {
  $baseArgs
}

Write-Host "Building butterfly-themed dejavu-Setup.exe installer..."
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -and $csc -ne $fwCsc) {
  Write-Warning "Roslyn csc.exe exited with $LASTEXITCODE; falling back to .NET Framework 4.8 csc.exe..."
  & $fwCsc @baseArgs
}
if ($LASTEXITCODE -ne 0) {
  throw "Setup compilation failed with exit code $LASTEXITCODE"
}

if ($env:DEJAVU_SIGN_PFX -and (Test-Path -LiteralPath $env:DEJAVU_SIGN_PFX)) {
  $signtool = $null
  $stCmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
  if ($stCmd) {
    $signtool = $stCmd.Source
  } else {
    $kitSignTool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
      Sort-Object FullName -Descending | Select-Object -First 1
    if ($kitSignTool) { $signtool = $kitSignTool.FullName }
  }
  if ($signtool) {
    Write-Host "Signing $setupExe with Authenticode (SHA-256 + RFC 3161 timestamp)..."
    $signArgs = @("sign", "/f", $env:DEJAVU_SIGN_PFX, "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256")
    if ($env:DEJAVU_SIGN_PASS) {
      $signArgs += @("/p", $env:DEJAVU_SIGN_PASS)
    }
    $signArgs += $setupExe
    & $signtool @signArgs
    if ($LASTEXITCODE -ne 0) {
      throw "signtool.exe failed with exit code $LASTEXITCODE"
    }
  } else {
    Write-Warning "DEJAVU_SIGN_PFX is set, but signtool.exe was not found; skipping Authenticode signing."
  }
}

$fi = Get-Item $setupExe
Write-Host ("Built installer successfully: {0} ({1:N0} KB)" -f $fi.FullName, ($fi.Length / 1024))
