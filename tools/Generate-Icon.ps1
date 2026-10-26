param([string]$PreviewPath)

# Run with Windows PowerShell in STA mode. The same vector also supplies the title-bar logo.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
$vectorPath = Join-Path $projectRoot 'Minimal\Assets\MinimalIcon.xaml'
$iconPath = Join-Path $projectRoot 'Minimal\Assets\Minimal.ico'
$resources = [System.Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText($vectorPath))
$logo = $resources['MinimalLogo']
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = [Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $height = $size * 0.88
        $width = $height * 18 / 20
        $rectangle = [System.Windows.Rect]::new(($size - $width) / 2, ($size - $height) / 2, $width, $height)
        $drawing.DrawImage($logo, $rectangle)
    }
    finally { $drawing.Close() }
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try {
        $encoder.Save($stream)
        $frames.Add($stream.ToArray())
    }
    finally { $stream.Dispose() }
}

$output = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0)  # Reserved
    $writer.Write([uint16]1)  # ICO
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
}
finally { $writer.Dispose() }

if ($PreviewPath) { [IO.File]::WriteAllBytes($PreviewPath, $frames[$frames.Count - 1]) }
Write-Output "Generated $iconPath ($($sizes -join ', ') pixels)."
