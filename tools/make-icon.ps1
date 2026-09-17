Add-Type -AssemblyName System.Drawing

$size = 64
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$m = 4
$r = 20
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($m, $m, $r, $r, 180, 90)
$path.AddArc($size - $m - $r, $m, $r, $r, 270, 90)
$path.AddArc($size - $m - $r, $size - $m - $r, $r, $r, 0, 90)
$path.AddArc($m, $size - $m - $r, $r, $r, 90, 90)
$path.CloseFigure()

$noteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(254, 243, 160))
$g.FillPath($noteBrush, $path)
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 185, 105), 3)
$g.DrawPath($borderPen, $path)

$inkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(62, 57, 38), 4)
$inkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$inkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawEllipse($inkPen, 17, 17, 30, 30)
$g.DrawLine($inkPen, 32, 32, 32, 21)
$g.DrawLine($inkPen, 32, 32, 40, 36)

$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Create((Join-Path $PSScriptRoot "..\app.ico"))
$icon.Save($fs)
$fs.Close()

$g.Dispose()
$bmp.Dispose()
Write-Host "OK: app.ico"
