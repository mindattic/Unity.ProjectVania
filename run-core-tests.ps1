# Runs the engine-free Core tests with dotnet test, outside Unity (U.VAN-US-A1).
param([string]$Filter = "")
$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "Tests/Core.Tests/Unity.Rea.ProjectVania.Core.Tests.csproj"
if ($Filter) { dotnet test $proj --filter $Filter } else { dotnet test $proj }
exit $LASTEXITCODE
