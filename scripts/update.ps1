#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('All', 'Codex', 'ClaudeCode')][string[]]$Target = @('All'),
    [string]$ProfileRoot = [Environment]::GetFolderPath('UserProfile')
)
$ErrorActionPreference = 'Stop'
$selected = if ($Target -contains 'All' -or $Target.Count -gt 1) { 'all' } elseif ($Target[0] -eq 'ClaudeCode') { 'claude' } else { 'codex' }
& node (Join-Path $PSScriptRoot 'skills.mjs') update --target $selected --profile $ProfileRoot
if ($LASTEXITCODE -ne 0) { throw 'Atualizacao nao concluida. As skills instaladas podem continuar na versao anterior.' }
