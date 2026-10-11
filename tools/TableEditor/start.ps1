# 룬 코드 테이블 편집기를 빌드·실행하고 브라우저로 연다.
param(
    [int]$Port = 8765
)
$toolDir = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $toolDir)
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Assets/RuneCode/Resources/RuneCode'))) { throw '저장소 루트를 찾지 못했습니다. tools/TableEditor 안에서 실행하세요.' }
Start-Process "http://127.0.0.1:$Port/"
Push-Location $toolDir
try { dotnet run --project $toolDir -- $Port }
finally { Pop-Location }
