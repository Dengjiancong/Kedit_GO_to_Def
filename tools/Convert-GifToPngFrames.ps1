[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputGif,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$Prefix = 'atri_cat',

    [ValidateRange(16, 2048)]
    [int]$Size = 180,

    [switch]$Overwrite
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = Get-Item -LiteralPath $InputGif -ErrorAction Stop
if ($source.Extension -ine '.gif') {
    throw 'Input file must be a GIF.'
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$gif = [System.Drawing.Image]::FromFile($source.FullName)
try {
    $dimension = [System.Drawing.Imaging.FrameDimension]::Time
    $frameCount = $gif.GetFrameCount($dimension)
    if ($frameCount -lt 1) {
        throw 'GIF has no exportable frames.'
    }

    $digits = [Math]::Max(2, ($frameCount - 1).ToString().Length)
    $names = for ($number = 0; $number -lt $frameCount; $number++) {
        '{0}_{1}.png' -f $Prefix, $number.ToString("D$digits")
    }

    # Avoid replacing existing animation assets unless explicitly requested.
    if (-not $Overwrite) {
        foreach ($name in $names) {
            $target = Join-Path $outputPath $name
            if (Test-Path -LiteralPath $target) {
                throw "Output file already exists: $target. Use -Overwrite to replace it."
            }
        }
    }

    if (-not (Test-Path -LiteralPath $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    }

    $delayBytes = $null
    try {
        $delayBytes = $gif.GetPropertyItem(0x5100).Value
    } catch {
        # Some GIFs omit frame delay metadata. Frames can still be exported.
    }

    $delayMs = New-Object System.Collections.Generic.List[int]
    $sourceWidth = $gif.Width
    $sourceHeight = $gif.Height
    $scale = [Math]::Min($Size / [double]$sourceWidth, $Size / [double]$sourceHeight)
    $drawWidth = [int][Math]::Round($sourceWidth * $scale)
    $drawHeight = [int][Math]::Round($sourceHeight * $scale)
    $drawX = [int][Math]::Floor(($Size - $drawWidth) / 2)
    $drawY = [int][Math]::Floor(($Size - $drawHeight) / 2)
    $rectangle = New-Object System.Drawing.Rectangle($drawX, $drawY, $drawWidth, $drawHeight)

    for ($number = 0; $number -lt $frameCount; $number++) {
        [void]$gif.SelectActiveFrame($dimension, $number)
        $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($gif, $rectangle)
            } finally {
                $graphics.Dispose()
            }

            $bitmap.Save((Join-Path $outputPath $names[$number]), [System.Drawing.Imaging.ImageFormat]::Png)
        } finally {
            $bitmap.Dispose()
        }

        if ($delayBytes -and $delayBytes.Length -ge (($number + 1) * 4)) {
            $delayMs.Add([System.BitConverter]::ToInt32($delayBytes, $number * 4) * 10)
        }
    }

    [pscustomobject]@{
        Source = $source.FullName
        OutputDirectory = $outputPath
        FrameCount = $frameCount
        SourceSize = '{0}x{1}' -f $sourceWidth, $sourceHeight
        OutputSize = '{0}x{0}' -f $Size
        Prefix = $Prefix
        DelayMs = if ($delayMs.Count) { $delayMs -join ',' } else { 'not provided' }
    }
} finally {
    $gif.Dispose()
}
