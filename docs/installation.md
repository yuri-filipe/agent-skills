# Instalação e migração

Execute os passos do README em um clone do repositório, com Node.js 22+.
O instalador funciona por cópia; não depende de links simbólicos nem privilégios
de administrador quando instalado no seu próprio perfil.

## Migração de skills já existentes

`install.ps1` faz uma verificação de todos os destinos antes de instalar.
Se uma pasta já existir sem `.agent-skills-install.json`, ela não será substituída.

1. Compare os arquivos existentes com `skills/<nome>` no clone.
2. Se forem idênticos, execute `./scripts/install.ps1 -Target All -AdoptExisting`.
3. Se forem diferentes, incorpore as alterações desejadas ao clone ou preserve a
   pasta existente em outro local. Mova somente a skill conflitante, não a raiz
   `.agents/skills` ou `.claude/skills`.
4. Execute novamente a instalação.

A importação inicial resumiu e ajustou as aspas do campo `description`, portanto algumas
cópias antigas podem diferir mesmo com instruções equivalentes. A adoção exige
igualdade dos arquivos, não apenas equivalência semântica.

## Backups e falhas

Antes de substituir uma pasta gerenciada, o instalador move a versão anterior para
`~/.agent-skills-backups/<nome>-<id>`. Backups não são removidos automaticamente.
Se a troca da pasta falhar, o instalador tenta restaurar o backup dessa skill.
Cada troca é individual: uma falha de disco durante a instalação pode deixar parte
das skills atualizadas. Resolva o erro e execute novamente. Evite instalações
simultâneas no mesmo perfil.

Para restaurar um backup, feche a sessão, preserve a pasta atualmente instalada
em outro local e mova o backup escolhido para o destino original. Não misture
arquivos de versões diferentes nem edite o marcador de instalação manualmente.

## Teste isolado

```powershell
./scripts/install.ps1 -Target All -ProfileRoot C:\Temp\agent-skills-demo
```

Escolha uma pasta nova. Esse comando não configura os aplicativos reais; só testa
as cópias em um perfil alternativo.
