#!/usr/bin/env pwsh
# ---------------------------------------------------------------------------
# Builds the Registry Server OCI image with podman, reusing deploy/docker/Dockerfile.
# The build context is the repository root. Run from anywhere.
#
#   pwsh deploy/podman/build.ps1
#   pwsh deploy/podman/build.ps1 -ImageTag mingo/service-registry:dev
#   pwsh deploy/podman/build.ps1 -ExtraNugetSource /path/to/local/artifacts
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$ImageTag = 'mingo/service-registry:0.1.0',
    [string]$ExtraNugetSource = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dockerfile = Join-Path $repoRoot 'deploy/docker/Dockerfile'

Write-Host "==> podman build (context: $repoRoot)" -ForegroundColor Cyan
Push-Location $repoRoot
try {
    if ($ExtraNugetSource) {
        podman build -f $dockerfile --build-arg "EXTRA_NUGET_SOURCE=$ExtraNugetSource" -t $ImageTag .
    }
    else {
        podman build -f $dockerfile -t $ImageTag .
    }
    if ($LASTEXITCODE -ne 0) { throw "podman build failed" }
}
finally { Pop-Location }

Write-Host "==> Built image: $ImageTag" -ForegroundColor Green
