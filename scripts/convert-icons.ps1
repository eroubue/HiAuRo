$ErrorActionPreference = "Stop"

$svgRoot = "E:\Dev\Game-Icon-Pack\svg-v1.0.3"
$pngRoot = Join-Path (Join-Path $PSScriptRoot "..") "HiAuRo\Resources\Icons"

$icons = @{
    "play"       = "2.Media & Technology\play.svg"
    "stop"       = "2.Media & Technology\stop.svg"
    "pause"      = "2.Media & Technology\pause.svg"
    "save"       = "1.UI\save.svg"
    "arrow-up"   = "4.Shapes & Symbol\arrow-up.svg"
    "arrow-down" = "4.Shapes & Symbol\arrow-down.svg"
    "cross"      = "1.UI\cross.svg"
}

$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

foreach ($name in $icons.Keys) {
    $srcSvg = Join-Path $svgRoot $icons[$name]

    $svgRelPath = $icons[$name]
    $category = (Split-Path $svgRelPath -Parent) -replace '^\d+\.', '' -replace ' ', '_' -replace '&', 'and'
    $outDir = Join-Path $pngRoot $category
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $outPng = Join-Path $outDir "$name.png"

    Write-Host "Converting: $srcSvg -> $outPng"
    $args = @($srcSvg, "-background", "none", "-resize", "48x48", $outPng)
    & magick $args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "magick failed for $name (exit code $LASTEXITCODE)"
    }
}

Write-Host "Done. $($icons.Count) icons converted."
