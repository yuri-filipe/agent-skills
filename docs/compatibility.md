# Compatibilidade

| Ambiente | Artefato | Atualização |
|---|---|---|
| Codex local | Pastas em `~/.agents/skills` | `update.ps1` |
| Claude Code local | Pastas em `~/.claude/skills` | `update.ps1` |
| Claude web/Cowork | ZIP individual da skill | Upload da nova versão na conta |
| Codex/OpenAI com plugins | ZIP `infinite-skills-codex-*` | Fluxo de instalação/publicação suportado pelo host |
| Claude com plugins | ZIP `infinite-skills-claude-*` | Fluxo de instalação suportado pelo host |

Todas as skills compartilham a estrutura Agent Skills (`SKILL.md`, referências e
templates). Os pacotes de plugins usam manifestos separados e o mesmo conteúdo.
Compatibilidade de formato não garante disponibilidade dos mesmos conectores,
bibliotecas privadas, scripts, rede ou execução em todos os aplicativos.

O pacote de plugin não cria marketplace nem publica automaticamente no diretório
público OpenAI. Também não conecta o GitHub como MCP. No ChatGPT, confirme quais
fluxos de skills/plugins estão disponíveis na conta; um ZIP de plugin para a API
não equivale a um upload universal na interface do ChatGPT.

Não instale a mesma skill como plugin e como pasta pessoal na mesma ferramenta,
pois podem surgir opções duplicadas. Skills já fornecidas por outro plugin da
Infinite precisam ser desabilitadas separadamente, se necessário.

## Fontes oficiais

- [Skills no Codex](https://developers.openai.com/pt-BR/docs/build-skills)
- [Empacotamento OpenAI](https://developers.openai.com/pt-BR/plugins/build/plugins)
- [Skills no Claude Code](https://code.claude.com/docs/en/skills)
- [Skills no Claude web](https://support.claude.com/en/articles/12512180-use-skills-in-claude)

Os testes deste repositório verificam arquivos, instalação e ZIPs. A descoberta e
o comportamento nos aplicativos devem ser verificados em sessões reais.
