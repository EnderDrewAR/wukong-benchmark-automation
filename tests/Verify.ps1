$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
dotnet build "$projectRoot\WukongBench" -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$resultText = & dotnet run --project "$projectRoot\WukongBench" -c Release --no-build -- parse "$PSScriptRoot\fixtures\reference-result.png"
if ($LASTEXITCODE -ne 0) { throw 'Result parsing failed' }
$result = ($resultText -join "`n") | ConvertFrom-Json
if ($result.AverageFps -ne 111 -or $result.MinimumFps -ne 95 -or $result.MaximumFps -ne 127 -or $result.FifthPercentileFps -ne 99) {
    throw 'OCR regression: values do not match the reference screenshot'
}
if ($result.ObservedFields.'Видеопамять' -ne '12GB' -or $result.ObservedFields.'Оперативная память' -ne '32GB') {
    throw 'Hardware field parsing regression'
}
Write-Host 'PASS: real benchmark screenshot parsed correctly (111 / 95 / 127 / 99 FPS, 12 GB VRAM, 32 GB RAM).'
$smallText = & dotnet run --project "$projectRoot\WukongBench" -c Release --no-build -- parse "$PSScriptRoot\fixtures\windowed-result.png"
if ($LASTEXITCODE -ne 0) { throw 'Windowed result parsing failed' }
$small = ($smallText -join "`n") | ConvertFrom-Json
if ($small.AverageFps -ne 209 -or $small.MinimumFps -ne 76 -or $small.MaximumFps -ne 276 -or $small.FifthPercentileFps -ne 186) {
    throw 'OCR regression at 1280x720'
}
Write-Host 'PASS: windowed 1280x720 result parsed correctly (209 / 76 / 276 / 186 FPS).'
