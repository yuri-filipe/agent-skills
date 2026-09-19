#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
& node (Join-Path $PSScriptRoot 'skills.mjs') validate
if ($LASTEXITCODE -ne 0) { throw 'Validacao falhou.' }
