# Atualizações

O GitHub é a fonte oficial. Skills locais e ZIPs são versões instaladas dessa fonte.
Use `./scripts/update.ps1 -Target All` antes de abrir uma nova sessão.

O atualizador exige `main`, clone limpo e origin HTTPS exatamente igual ao README.
Recusa histórico divergente, alterações locais e conflitos nas skills instaladas.
Após atualizar o clone, ele instala as dependências com `npm ci --ignore-scripts`
e inicia um novo processo com o instalador da revisão recebida.

Uma falha de rede/dependências antes da instalação preserva as skills instaladas;
o clone pode já estar atualizado. Verifique o erro e tente novamente.

Não há tarefa agendada, hook global ou consulta de rede em cada mensagem. Para
usar sempre a revisão atual, conclua a atualização antes de iniciar o trabalho.
Sem rede, uma cópia local continua utilizável, mas não pode ser chamada de atualizada.

## Versão fixa

Em um clone limpo, selecione uma tag existente:

```powershell
git fetch --tags origin
git switch --detach v1.0.0
npm ci --ignore-scripts
./scripts/install.ps1 -Target All
```

Esse exemplo exige que a tag tenha sido publicada. Para voltar ao acompanhamento
de `main`, use `git switch main` e o atualizador. Backups e detecção de conflitos
também se aplicam ao downgrade.

## Edição

Edite o clone, envie uma branch/PR e integre no GitHub. Não edite a cópia instalada.
Alterações nas dependências e scripts recebidas do repositório serão executadas
na atualização; mantenha controle de escrita do repositório e revise contribuições.
