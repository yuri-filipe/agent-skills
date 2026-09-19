# Manutenção deste repositório

- Edite as skills apenas em `skills/`; pacotes em `dist/` são gerados e não versionados.
- Preserve templates com placeholders `{{...}}`: são modelos, não código incompleto.
- Mantenha as instruções portáveis e os caminhos de recursos relativos à skill.
- Não transforme pedidos de diagnóstico em autorização para implementar mudanças.
- Não copie tokens ou configurações pessoais para exemplos.
- Valide com `npm run validate`, `npm test` e `npm run package`.
- Teste instalações com um perfil temporário, nunca modificando o perfil real durante testes.
- As versões em package.json e packaging/*/plugin.json devem ser iguais.
- Não afirme compatibilidade operacional com uma interface sem testar a instalação nela.
- Não remova nem sobrescreva skills não gerenciadas pelo instalador.
