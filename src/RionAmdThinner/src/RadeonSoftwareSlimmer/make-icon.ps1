# Generates icon.ico — a bold "R" (the Rion logo) on an accent rounded square.
Add-Type -AssemblyName System.Drawing

$sizes  = 16,24,32,48,64,128,256
$accent = [System.Drawing.Color]::FromArgb(255, 228, 0, 43)   # Rion AMD Thinner red
$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$blobs  = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded-square background
    $pad    = [Math]::Max(1, [int]($s * 0.06))
    $radius = [int]($s * 0.22)
    $rect   = New-Object System.Drawing.Rectangle $pad, $pad, ($s - 2*$pad), ($s - 2*$pad)
    $path   = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush $accent
    $g.FillPath($brush, $path)

    # bold R
    $fontSize = [single]($s * 0.66)
    $font = $null
    foreach ($fam in 'Segoe UI Black','Arial Black','Segoe UI','Arial') {
        try { $font = New-Object System.Drawing.Font $fam, $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel); break } catch {}
    }
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $textRect = New-Object System.Drawing.RectangleF 0, ([single](-$s*0.04)), $s, $s
    $g.DrawString('R', $font, [System.Drawing.Brushes]::White, $textRect, $sf)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $blobs += ,($ms.ToArray())
}

# assemble .ico (PNG-compressed entries, valid on Vista+)
$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $blobs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($b in $blobs) { $bw.Write($b) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'icon.ico'), $ico.ToArray())
Write-Host "wrote $(Join-Path $outDir 'icon.ico')  ($($ico.Length) bytes)"
