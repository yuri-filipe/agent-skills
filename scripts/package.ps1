#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
& node (Join-Path $PSScriptRoot 'skills.mjs') package
if ($LASTEXITCODE -ne 0) { throw 'Empacotamento falhou.' }
