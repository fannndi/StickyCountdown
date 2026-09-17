$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$csc64 = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$csc32 = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$csc = if (Test-Path $csc64) { $csc64 } else { $csc32 }

$out = Join-Path $root "StickyCountdown.exe"
$src = Join-Path $root "src\StickyCountdown.cs"
$icon = Join-Path $root "app.ico"

$args = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/out:$out",
    "/r:System.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:Microsoft.VisualBasic.dll"
)

if (Test-Path $icon) { $args += "/win32icon:$icon" }
$args += $src

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "Compile gagal (exit code $LASTEXITCODE)" }
Write-Host "OK: $out"
