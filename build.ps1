$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $Root "src"
$DistDir = Join-Path $Root "dist"
$OutExe = Join-Path $DistDir "StickyNotes.exe"

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $Csc)) {
    throw "csc.exe not found at $Csc"
}

$FrameworkDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$WpfDir = Join-Path $FrameworkDir "WPF"

$References = @(
    (Join-Path $WpfDir "PresentationFramework.dll"),
    (Join-Path $WpfDir "PresentationCore.dll"),
    (Join-Path $WpfDir "WindowsBase.dll"),
    (Join-Path $FrameworkDir "System.Xaml.dll"),
    (Join-Path $FrameworkDir "System.Runtime.Serialization.dll"),
    (Join-Path $FrameworkDir "System.Drawing.dll"),
    (Join-Path $FrameworkDir "System.Windows.Forms.dll")
)

$Sources = Get-ChildItem -Path $SrcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

$Args = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/out:$OutExe"
)
foreach ($ref in $References) {
    $Args += "/reference:$ref"
}
$Args += $Sources

Write-Host "Building StickyNotes.exe..."
& $Csc @Args
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$SizeKB = [math]::Round((Get-Item $OutExe).Length / 1024, 1)
Copy-Item -Path (Join-Path $Root "UninstallStickyNotes.bat") -Destination (Join-Path $DistDir "UninstallStickyNotes.bat") -Force
Write-Host "Build succeeded: $OutExe ($SizeKB KB)"
