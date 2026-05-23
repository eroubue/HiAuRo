$ErrorActionPreference = "Stop"
$svgRoot = "E:\Dev\Game-Icon-Pack\svg-v1.0.3"
$workDir = Join-Path $PSScriptRoot "font-gen"
$fullDir = Join-Path $workDir "full-icons"
$outputDir = Join-Path $workDir "full-output"

Remove-Item -Recurse -Force $fullDir, $outputDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $fullDir | Out-Null
New-Item -ItemType Directory -Force $outputDir | Out-Null

$pairs = @(
    @("UI", "1.UI"),
    @("Media_Technology", "2.Media & Technology"),
    @("Editing_Tools", "3.Editing Tools"),
    @("Shapes_Symbol", "4.Shapes & Symbol"),
    @("Game", "5.Game"),
    @("Items", "6.Items")
)

$codepoint = 0xEA00

foreach ($pair in $pairs) {
    $catPrefix = $pair[0]
    $catDir = $pair[1]
    $srcDir = Join-Path $svgRoot $catDir
    Get-ChildItem $srcDir -Filter "*.svg" | ForEach-Object {
        $uniqueName = "${catPrefix}_$($_.BaseName)"
        $destPath = Join-Path $fullDir "$uniqueName.svg"
        Copy-Item $_.FullName $destPath -Force
        Write-Output "$uniqueName -> U+$($codepoint.ToString('X4'))"
        $codepoint++
    }
}

$total = $codepoint - 0xEA00
Write-Host "Total: $total icons copied"

if ($total -gt 0) {
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    npx svgtofont -s $fullDir -o $outputDir -f "game-icons" 2>&1
    Write-Host "Done. TTF at $outputDir\game-icons.ttf"
}
