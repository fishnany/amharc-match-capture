$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solution = Join-Path $repoRoot "agent-windows\AmharcAgent.sln"

if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw "AMHARC Capture solution was not found at expected path: $solution"
}

dotnet test $solution @args

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
