# Cenários manuais de aceitação

Use um projeto de teste adequado, com as dependências e bibliotecas disponíveis.
Nenhum teste deve publicar, implantar ou mudar bancos sem que isso faça parte do pedido.

| Skill | Pedido positivo | Pedido negativo | Resultado observável |
|---|---|---|---|
| infinite-dotnet-api | Criar módulo de produtos nesta API Infinite | Explicar um SELECT Oracle | Usa estrutura e templates CQRS quando solicitado |
| infinite-api-logs | Adicionar logs a este handler | Configurar um servidor de observabilidade | Logs estruturados sem dados sensíveis e sem mudar regra de negócio |
| infinite-auth-zitadel | Proteger endpoints desta API com Zitadel | Traduzir um texto | Contrato da biblioteca e configuração necessária explicitados |
| infinite-angular-build | Revisar o build deste Angular na pipeline Infinite | Configurar build Vite | Identifica outputPath e contrato do Dockerfile centralizado |
| frontend-remote-config | Migrar este Vite para config do Consul | Configurar autenticação da API | Configuração síncrona com os templates e contrato da pipeline |
| infinite-frontend-datetime | Ajustar este formulário de agendamento para o fuso do evento | Configurar a pipeline do Angular | Envia instantes UTC com `Z`, mantém datas civis sem conversão |

Registre aplicativo/versão, commit da skill, entrada, evidência e resultado ao
executar esses cenários. A importação inicial não significa que todos já passaram.
