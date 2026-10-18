# REA GUI icon generator: render a high-res master, scale to sizes, pack PNG-in-ICO.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$outDir = $PSScriptRoot
$masterSize = 512

function New-Master([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size
    $pad = [int]($s * 0.06)
    $rect = New-Object System.Drawing.Rectangle($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $radius = [int]($s * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90) | Out-Null
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90) | Out-Null
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90) | Out-Null
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90) | Out-Null
    $path.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush( `
        (New-Object System.Drawing.PointF(0,0)), (New-Object System.Drawing.PointF($s,$s)), `
        [System.Drawing.Color]::FromArgb(255,13,17,23), [System.Drawing.Color]::FromArgb(255,22,27,34))
    $g.FillPath($brush, $path)
    $brush.Dispose()

    $borderBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush( `
        (New-Object System.Drawing.PointF(0,0)), (New-Object System.Drawing.PointF($s,$s)), `
        [System.Drawing.Color]::FromArgb(255,34,211,238), [System.Drawing.Color]::FromArgb(255,168,85,247))
    $borderPen = New-Object System.Drawing.Pen($borderBrush, [single]([Math]::Max(2, $s * 0.02)))
    $g.DrawPath($borderPen, $path)
    $borderPen.Dispose(); $borderBrush.Dispose()

    $cx = [int]($s * 0.44); $cy = [int]($s * 0.44); $cr = [int]($s * 0.24)
    $ringRect = New-Object System.Drawing.Rectangle(($cx - $cr), ($cy - $cr), ($cr * 2), ($cr * 2))
    $cyan = [System.Drawing.Color]::FromArgb(255,34,211,238)
    $violet = [System.Drawing.Color]::FromArgb(255,168,85,247)
    $ringBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush( `
        (New-Object System.Drawing.PointF($ringRect.X,$ringRect.Y)), `
        (New-Object System.Drawing.PointF($ringRect.Right,$ringRect.Bottom)), $cyan, $violet)
    $ringPen = New-Object System.Drawing.Pen($ringBrush, [single]([Math]::Max(3, $s * 0.045)))
    $g.DrawEllipse($ringPen, $ringRect)
    $ringPen.Dispose(); $ringBrush.Dispose()

    $hx1 = [int]($cx + $cr * 0.72); $hy1 = [int]($cy + $cr * 0.72)
    $hx2 = [int]($cx + $cr * 1.5);  $hy2 = [int]($cy + $cr * 1.5)
    $handlePen = New-Object System.Drawing.Pen($violet, [single]([Math]::Max(4, $s * 0.06)))
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($handlePen, $hx1, $hy1, $hx2, $hy2)
    $handlePen.Dispose()

    $font = New-Object System.Drawing.Font("Consolas", [single]($s * 0.22), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $txtRect = New-Object System.Drawing.RectangleF($ringRect.X, $ringRect.Y, $ringRect.Width, $ringRect.Height)
    $g.DrawString(">_", $font, [System.Drawing.Brushes]::White, $txtRect, $fmt)
    $font.Dispose()

    $dotFont = New-Object System.Drawing.Font("Consolas", [single]($s * 0.08), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $muted = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200,139,148,158))
    $g.DrawString("01", $dotFont, $muted, [single]($s * 0.70), [single]($s * 0.70)) | Out-Null
    $g.DrawString("AF", $dotFont, $muted, [single]($s * 0.70), [single]($s * 0.10)) | Out-Null
    $dotFont.Dispose(); $muted.Dispose()

    $g.Dispose()
    return $bmp
}

$master = New-Master $masterSize
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBlobs = @()
foreach ($sz in $sizes) {
    $small = New-Object System.Drawing.Bitmap($sz, $sz)
    $gg = [System.Drawing.Graphics]::FromImage($small)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gg.DrawImage($master, 0, 0, $sz, $sz)
    $gg.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $small.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += ,($ms.ToArray())
    $ms.Dispose(); $small.Dispose()
}

$pngPath = Join-Path $outDir "app.png"
$master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()

$icoPath = Join-Path $outDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $blob = $pngBlobs[$i]
    $wByte = if ($sz -ge 256) { 0 } else { $sz }
    $hByte = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([Byte]$wByte); $bw.Write([Byte]$hByte); $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$blob.Length); $bw.Write([UInt32]$offset)
    $offset += $blob.Length
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }
$bw.Flush(); $bw.Close(); $fs.Close()
Write-Host ("Icon written: " + $pngPath + " and " + $icoPath)
