#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('All', 'Codex', 'ClaudeCode')][string[]]$Target = @('All'),
    [string]$ProfileRoot = [Environment]::GetFolderPath('UserProfile'),
    [switch]$AdoptExisting
)
$ErrorActionPreference = 'Stop'
$selected = if ($Target -contains 'All' -or $Target.Count -gt 1) { 'all' } elseif ($Target[0] -eq 'ClaudeCode') { 'claude' } else { 'codex' }
$arguments = @((Join-Path $PSScriptRoot 'skills.mjs'), 'install', '--target', $selected, '--profile', $ProfileRoot)
if ($AdoptExisting) { $arguments += '--adopt' }
& node @arguments
if ($LASTEXITCODE -ne 0) { throw 'Instalacao nao concluida. Consulte o erro acima.' }
