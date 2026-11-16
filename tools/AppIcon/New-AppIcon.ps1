<#
.SYNOPSIS
    Draws the application icon and writes it as a multi-size .ico file.

.DESCRIPTION
    The icon is a battery on a rounded tile, in the proportions of the notification-area icon the app draws
    at run time (src/Desktop/Services/TrayIconImage.cs). The tile fades into the accent of
    src/Desktop/Styling/Palette.xaml, the hollow is its window colour and the charge its positive tone; the
    lighter top of the tile and the near-white outline are the icon's own.
    Every size is drawn on its own pixel grid rather than scaled down from the largest, so the small ones stay sharp.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/AppIcon/New-AppIcon.ps1
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\src\Desktop\Assets\App.ico'),

    # Also saves each size as a PNG here, to look at the result without an icon viewer.
    [string]$PreviewDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# .NET resolves a relative path against the folder the process started in, not the current location.
$OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ($PreviewDirectory) {
    $PreviewDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PreviewDirectory)
}

$GridSize = 16.0
$Sizes = 16, 20, 24, 32, 40, 48, 64, 256

# Windows reads a PNG frame only at this size; smaller frames have to be bitmaps.
$PngFrameSize = 256

$TileTopColor = [System.Drawing.ColorTranslator]::FromHtml('#2E79A3')
$TileBottomColor = [System.Drawing.ColorTranslator]::FromHtml('#1F3A4D')
$OutlineColor = [System.Drawing.ColorTranslator]::FromHtml('#F2F4F6')
$HollowColor = [System.Drawing.ColorTranslator]::FromHtml('#0E1116')
$ChargeColor = [System.Drawing.ColorTranslator]::FromHtml('#4CAF50')
$ChargedShare = 0.68

function New-RoundedRectangle([System.Drawing.Rectangle]$bounds, [int]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($radius -le 0) {
        $path.AddRectangle($bounds)
        return $path
    }

    $diameter = 2 * $radius
    $path.AddArc($bounds.Left, $bounds.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bounds.Left, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill([System.Drawing.Graphics]$graphics, [System.Drawing.Brush]$brush, [System.Drawing.Rectangle]$bounds, [int]$radius) {
    $path = New-RoundedRectangle $bounds $radius
    $graphics.FillPath($brush, $path)
    $path.Dispose()
}

function New-Frame([int]$size) {
    $unit = $size / $GridSize
    $scaled = { param($units) [int][Math]::Round($units * $unit, [MidpointRounding]::AwayFromZero) }

    $stroke = [Math]::Max(1, (& $scaled 1))
    $gap = [int][Math]::Floor($unit / 2)
    $isSmall = $size -lt 32

    $tile = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $body = New-Object System.Drawing.Rectangle (& $scaled 2), (& $scaled 5), (& $scaled 11), (& $scaled 6)
    $terminal = New-Object System.Drawing.Rectangle $body.Right, ($body.Top + [int]($body.Height / 4)), $stroke, ([int]($body.Height / 2))
    $hollow = [System.Drawing.Rectangle]::Inflate($body, -$stroke, -$stroke)
    $cell = [System.Drawing.Rectangle]::Inflate($hollow, -$gap, -$gap)
    $cell.Width = [Math]::Max(1, [int][Math]::Round($cell.Width * $ChargedShare))

    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $tile, $TileTopColor, $TileBottomColor, 90.0
    Fill $graphics $tileBrush $tile (& $scaled 3.5)
    $tileBrush.Dispose()

    $outlineBrush = New-Object System.Drawing.SolidBrush $OutlineColor
    $hollowBrush = New-Object System.Drawing.SolidBrush $HollowColor
    $chargeBrush = New-Object System.Drawing.SolidBrush $ChargeColor

    # Round corners a pixel or two wide only blur a small frame.
    $bodyRadius = if ($isSmall) { 0 } else { & $scaled 1.25 }
    $innerRadius = if ($isSmall) { 0 } else { & $scaled 0.5 }

    Fill $graphics $outlineBrush $body $bodyRadius
    $graphics.FillRectangle($outlineBrush, $terminal)
    Fill $graphics $hollowBrush $hollow $innerRadius
    Fill $graphics $chargeBrush $cell ([Math]::Max(0, $innerRadius - $gap))

    $outlineBrush.Dispose()
    $hollowBrush.Dispose()
    $chargeBrush.Dispose()
    $graphics.Dispose()
    return $bitmap
}

function ConvertTo-PngFrame([System.Drawing.Bitmap]$bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

# A device-independent bitmap as an icon holds it: twice the height in the header, rows bottom-up, then
# a one-bit mask that the alpha channel makes redundant but the format still requires.
function ConvertTo-BitmapFrame([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $bounds = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bitmap.LockBits($bounds, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rowLength = $size * 4
    $pixels = New-Object byte[] ($rowLength * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bitmap.UnlockBits($data)

    $maskRowLength = [int][Math]::Ceiling($size / 32.0) * 4
    $mask = New-Object byte[] ($maskRowLength * $size)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([int]40)
    $writer.Write([int]$size)
    $writer.Write([int](2 * $size))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int]($pixels.Length + $mask.Length))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    for ($row = $size - 1; $row -ge 0; $row--) {
        $writer.Write($pixels, $row * $rowLength, $rowLength)
    }

    $writer.Write($mask)
    $writer.Flush()
    return , $stream.ToArray()
}

$frames = foreach ($size in $Sizes) {
    $bitmap = New-Frame $size

    if ($PreviewDirectory) {
        New-Item -ItemType Directory -Force $PreviewDirectory | Out-Null
        $bitmap.Save((Join-Path $PreviewDirectory "App-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $bytes = if ($size -ge $PngFrameSize) { ConvertTo-PngFrame $bitmap } else { ConvertTo-BitmapFrame $bitmap }
    $bitmap.Dispose()

    [pscustomobject]@{ Size = $size; Bytes = $bytes }
}

$HeaderLength = 6
$EntryLength = 16

$icon = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $icon
$writer.Write([int16]0)
$writer.Write([int16]1)
$writer.Write([int16]$frames.Count)

$offset = $HeaderLength + $EntryLength * $frames.Count
foreach ($frame in $frames) {
    # A side of 256 does not fit a byte and is written as zero.
    $side = [byte]($frame.Size % 256)
    $writer.Write($side)
    $writer.Write($side)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]$frame.Bytes.Length)
    $writer.Write([int]$offset)
    $offset += $frame.Bytes.Length
}

foreach ($frame in $frames) {
    $writer.Write([byte[]]$frame.Bytes)
}

$writer.Flush()

$target = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[System.IO.File]::WriteAllBytes($target, $icon.ToArray())

Write-Output "${target}: $($frames.Count) sizes, $($icon.Length) bytes"
