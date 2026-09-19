# Contribuir

1. Crie uma branch `feature/<assunto>` ou `fix/<assunto>`.
2. Edite `skills/<nome>/SKILL.md` e apenas os recursos necessários.
3. Use YAML válido com `name` igual ao nome da pasta e `description` concisa.
4. Inclua na descrição do PR um exemplo que deve ativar a skill e outro que não deve.
5. Execute `npm ci --ignore-scripts`, `npm run validate`, `npm test` e `npm run package`.
6. Abra um PR para `main` e verifique o workflow Windows/Linux.

O validador lê YAML, confere nomes, recursos citados em links Markdown e caminhos
de recursos em backticks, versões dos manifestos e alguns padrões de credenciais.
Não é uma auditoria completa de segredos nem valida semanticamente instruções,
código .NET, pipelines externos ou todos os caminhos mencionados na prosa.

Os testes Node validam conflitos, backups, substituição de arquivos gerenciados e
conteúdo dos ZIPs. Testes de comportamento de skills requerem uso representativo
no aplicativo de destino. Ver `docs/scenarios.md`.

Não escolha ou altere a licença sem decisão do autor.
