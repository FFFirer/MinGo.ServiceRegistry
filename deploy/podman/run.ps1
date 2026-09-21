#!/usr/bin/env pwsh
# ---------------------------------------------------------------------------
# Runs the Registry Server OCI image with podman, mapping a host port to the
# container port 8080. Defaults map host 5080 -> container 8080.
#
#   pwsh deploy/podman/run.ps1
#   pwsh deploy/podman/run.ps1 -HostPort 8080
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$ImageTag = 'mingo/service-registry:0.1.0',
    [int]$HostPort = 5080
)

$ErrorActionPreference = 'Stop'

Write-Host "==> podman run $ImageTag (http://localhost:$HostPort -> container :8080)" -ForegroundColor Cyan
podman run --rm -p "${HostPort}:8080" $ImageTag
