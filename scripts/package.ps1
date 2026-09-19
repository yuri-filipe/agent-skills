#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
& node (Join-Path $PSScriptRoot 'skills.mjs') package
if ($LASTEXITCODE -ne 0) { throw 'Empacotamento falhou.' }
