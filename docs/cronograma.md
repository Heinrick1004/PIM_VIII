# Cronograma planejado — 09 a 22/11/2026

Este é um plano interno de duas semanas, elaborado em 09/11/2026, e não uma declaração da data oficial de entrega da UNIP. Os dias de fim de semana constam do plano e dependem da disponibilidade acordada pela equipe. O progresso real e os impedimentos devem ser registrados no [diário de desenvolvimento](diario-desenvolvimento.md).

| Dia | Data | Sprint | Atividade planejada | Evidência esperada |
| --- | --- | --- | --- | --- |
| 1 | 09/11, segunda-feira | S1 | Diagnosticar ambiente e arquivos; completar solução, domínio, API mínima, SQL de referência, testes e planejamento. | Arquivos revisados; restore/build/testes e HTTP registrados; estado Git conhecido. |
| 2 | 10/11, terça-feira | S1 | Reavaliar atualização para .NET 10 antes do fim do suporte .NET 8; confirmar schema; preparar EF Core/SQL Server e primeira migration revisada. | Decisão de versão registrada com testes; migration aplicada em desenvolvimento real; constraints/rowversion verificadas; conexão sem segredo versionado. |
| 3 | 11/11, quarta-feira | S1 | Implementar autenticação e autorização por perfis. | Hashing apropriado, identidade no servidor e testes de credenciais/401/403. |
| 4 | 12/11, quinta-feira | S1 | Implementar gestão administrativa de usuários e criação/atribuição de tarefas, incluindo histórico transacional (antecipado). | Casos de uso persistidos, unicidade de e-mail e regras de cadastro testadas. |
| 5 | 13/11, sexta-feira | S1 | Consultar histórico, fechar Sprint 1 e registrar retrospectiva (antecipado). | Consulta segura e paginada; revisão US01–US05 e pendências documentadas. |
| 6 | 14/11, sábado | S2 | Implementar consultas paginadas e filtros com visibilidade por usuário. | Testes de permissões e consultas em SQL Server. |
| 7 | 15/11, domingo | S2 | Implementar alterações, concorrência otimista e histórico. | Dois escritores testados; conflito sem sobrescrita; auditoria consistente. |
| 8 | 16/11, segunda-feira | S2 | Implementar web responsiva com autenticação e consulta. | Web integrada à API, fluxo e exibição em tamanhos de tela previstos. |
| 9 | 17/11, terça-feira | S2 | Concluir atualização de status na web; preparar procedures, functions e trigger com finalidade definida. | Fluxo web real e scripts SQL sem duplicar histórico. |
| 10 | 18/11, quarta-feira | S2 | Testar objetos SQL e transações; revisar incremento e retrospectiva S2. | Evidências SQL, testes integrados e backlog ajustado. |
| 11 | 19/11, quinta-feira | S3 | Implementar Flutter mínimo consumindo API. | Login, consulta e atualização de status demonstrados no cliente. |
| 12 | 20/11, sexta-feira | S3 | Implementar desktop C# mínimo; preparar imagem Docker. | Cliente desktop integrado e build da imagem registrado. |
| 13 | 21/11, sábado | S3 | Preparar Kubernetes mínimo e CI/CD; executar validação integrada. | Pipeline executado, configuração/probes verificadas e evidência de implantação quando disponível. |
| 14 | 22/11, domingo | S3 | Reserva para correções; revisar requisitos, documentação e demonstração; retrospectiva S3. | Relatório de verificação, pendências explícitas e roteiro de demonstração. |

## Atualização de execução — 10/11/2026

O incremento de autenticação previsto para o Dia 3 foi antecipado e implementado em 10/11: JWT, hashing, autorização por perfil, bootstrap administrativo explícito, endpoints de usuários, testes e documentação. Isso não altera a data planejada dos dias posteriores nem declara concluídas as funcionalidades ainda pendentes. O bootstrap manual e a demonstração em `TaskFlowDev` dependem de senha escolhida pelo mantenedor e permanecem por executar.

O incremento do Dia 4 também foi antecipado para 10/11: criação e atribuição administrativa de tarefas, histórico na mesma transação, consulta de detalhe com autorização por criador/responsável e testes HTTP/SQL Server. A lista paginada e as atualizações/reatribuições permanecem para etapas posteriores. A validação não gravou em `TaskFlowDev` nem alterou migrations.

O Dia 5 foi implementado em 10/11, também antecipadamente: consulta do histórico paginada, ordenada por data e Id e protegida pela mesma regra de vínculo da tarefa. A revisão da Sprint 1 foi registrada no backlog e no diário; US01–US05 atendem seus critérios documentados com evidência automatizada, e a demonstração manual permanece explicitamente pendente. A Sprint 1 foi fechada antes do término planejado de 13/11. Para a Sprint 2, priorizar listagem geral/filtros e atualização com RowVersion/histórico; reavaliar o escopo de web e programação SQL conforme capacidade.

O Dia 6 foi implementado em 10/11: `GET /api/tasks` filtra e pagina no SQL Server com escopo de administrador/funcionário aplicado antes das demais operações da query. Foram adicionados filtros combináveis, ordenação determinística e testes HTTP/SQL Server em banco isolado. US06 foi concluída; a Sprint 2 segue aberta para atualização concorrente, clientes e demais entregas.

O Dia 7 foi concluído em 11/11: atualização de status com autorização por responsável atual, edição administrativa parcial, reatribuição/remoção, RowVersion aplicado como token concorrente efetivo do EF e histórico transacional. Testes HTTP com SQL Server real isolado comprovaram 409 sob corrida de dois DbContexts, preservação da gravação vencedora, rollback de histórico, permissões e visibilidade após reatribuição. US07 foi concluída; a Sprint 2 continua em andamento para US08–US09. `TaskFlowDev` não foi acessado, e a demonstração manual permanece pendente.

## Dependências e riscos de prazo

- O Dia 2 depende de acesso a uma instância real de SQL Server. Não substituir persistência por respostas estáticas para apresentar conclusão.
- O projeto foi migrado e validado em .NET 10 no Dia 2; não permanece em .NET 8.
- A autenticação deve preceder a exposição de operações de usuários e tarefas. Os clientes dependem dos contratos autenticados da API.
- A decisão de schema e a estratégia de auditoria devem preceder migrations e triggers, evitando alterações concorrentes ou registros duplicados.
- Docker, cluster e credenciais de implantação podem exigir preparação externa. Diretórios e manifests existentes não comprovam implantação; registrar o que foi executado.
- A janela é curta para três clientes e infraestrutura. Revisar capacidade diariamente, reservar o último dia para correções e manter os clientes mínimos.

O plano pode ser reordenado conforme impedimentos reais, preservando dependências, critérios de aceite e registro das decisões. Datas planejadas não devem ser reescritas como execução concluída sem evidências.
