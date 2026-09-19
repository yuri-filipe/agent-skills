#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
& node (Join-Path $PSScriptRoot 'skills.mjs') validate
if ($LASTEXITCODE -ne 0) { throw 'Validação falhou.' }
