# Auditoria de evidências do PIM VIII

Auditoria documental e estática em **17/11/2026**. Não houve conexão a banco, execução de migration nem teste de integração SQL nesta auditoria. O Manual.pdf não foi consultado; os requisitos do manual abaixo seguem a lista fornecida pelo responsável nesta tarefa. Resultados anteriores são explicitamente identificados como históricos.

## Banco de dados

| Requisito | Implementação encontrada | Evidência disponível | Situação nesta auditoria |
|---|---|---|---|
| Stored procedure | `dbo.usp_TarefasResumo` na migration `20261113090000_AddTaskFlowProgrammability`; invocada por `TarefaRepository.GetSummaryAsync` com parâmetros tipados e `CommandType.StoredProcedure`. | `SqlServerProgrammabilityTests.FunctionProcedureAndTrigger_EnforceExpectedBehaviorOnSqlServer` verifica totais globais, escopo e conjunto vazio; resultado SQL documentado em 13/11/2026 (suíte 123/123). | Implementada e historicamente testada; não reexecutada nesta auditoria. |
| Function | `dbo.fn_TarefasVisiveis` retorna IDs criados pelo usuário ou atribuídos; usada pela procedure de resumo. A listagem API aplica visibilidade diretamente em LINQ/SQL. | Mesmo teste consulta a function diretamente para criador, responsável, usuário sem vínculos e GUID vazio. | Implementada e historicamente testada. Uso pela procedure confirmado no código; a lista de tarefas não depende da function. |
| Trigger | `dbo.TR_Tarefas_ProtegeDadosCriacao`, `AFTER UPDATE`, compara `inserted`/`deleted` e rejeita mudança em criador/data de criação. Não grava histórico. | Teste SQL tenta mudar cada campo e exige erro 51001; update permitido de outros campos e teste de migração Down/Up. A migration cria trigger habilitada por padrão. | Implementada e historicamente testada em fixture SQL. Estado habilitado do banco de produção/demonstração não foi consultado agora. |
| Transações e atomicidade | `TaskFlowUnitOfWork`; endpoints envolvem gravação da tarefa e eventos de histórico em transação, com Serializable para atualizações concorrentes e bootstrap. | Testes `Admin_CreatesTaskWithRealCreatorAssignmentHistoryAndRowVersion`, `Create_RollsBackTaskWhenHistoryPersistenceFails`, `UpdateAndHistory_CommitTogetherAndSameValueDoesNotDuplicateHistory`, `UpdateAndHistory_RollsBackTaskWhenHistoryViolatesForeignKey` e testes de endpoint de rollback. | Implementação confirmada por leitura; evidência de execução é histórica (SQL isolado). |
| Concorrência otimista | Coluna `Versao` rowversion, mapeada via `IsRowVersion`; cliente transmite Base64 de 8 bytes; EF usa token original e API converte concorrência em 409. | `Tarefa_RowVersionRejeitaEscritaConcorrenteSemSobrescrever` e `TaskStatusUpdate_UsesSqlServerRowVersionForConcurrentHttpConflict`. | Implementada e historicamente testada em SQL Server isolado. |
| Índices e integridade | PKs, FK indexes convencionais; índice único em `Usuarios.Email`; `IX_Tarefas_Status_ResponsavelId`; índices de FK de histórico; CHECK constraints de IDs, enum e campos obrigatórios. | Fluent configurations e `TaskFlowDbContextModelSnapshot`; testes históricos de constraints e persistência. | Estrutura encontrada; não executada consulta de plano/estatísticas nesta auditoria. |
| Permissões e acesso | API deriva identidade/perfil de JWT revalidado; filtros de visibilidade aplicados antes de count/paginação; detalhe/histórico verificam vínculo. Function limita visibilidade, mas não substitui autorização da API. | Testes HTTP/SQL de escopo, administrador, criador, responsável e usuário não vinculado; evidência histórica. | Código estático consistente; permissões em banco não foram exercitadas agora. Não há clientes com acesso direto ao SQL no desenho. |
| Paginação/consulta | Listas de tarefa, histórico e usuário fazem `COUNT`, filtros e `Skip/Take` no EF antes da materialização, `AsNoTracking` e desempate determinístico por data/nome + ID. | `TaskList_AppliesVisibilityFiltersAndPaginationInSqlServer`, `History_PaginatesInUtcOrderWithIdAsDeterministicTieBreaker`, persistência filtrada/paginada; históricos no diário. | Código confirma execução server-side; sem benchmark atual. |
| Alto volume | Há paginação e índices de partida, mas não há benchmark, plano de execução, carga representativa ou SLO registrados. `Titulo.Contains` tende a gerar busca com curinga inicial e não é demonstrado como seek por índice B-tree. | Nenhuma medição encontrada nos documentos/código inspecionados. | Não declarar preparado/comprovado para alto volume. Avaliar índice full-text/estratégia de busca somente com perfil de carga e plano reais. Nenhuma mudança SQL proposta nesta etapa. |
| Segurança SQL | Constraints, parâmetros SQL tipados na procedure, hashes fora das respostas e autorização na API. | Testes de autorização e ausência de segredos documentados. | Não foi encontrada evidência versionada de grants por principal, rotação de credenciais, política de acesso mínimo ao SQL ou auditoria operacional; documentar/configurar no ambiente de implantação. |

### Segurança de testes de banco

`SqlServerTestDatabase` cria um catálogo `TaskFlowIntegrationTests_<GUID>`, migra-o, reseta dados próprios e remove esse catálogo ao final. Esse fluxo é destrutivo para a base temporária por projeto e foi deliberadamente **não executado** nesta auditoria. Não conectei a `TaskFlowDev` ou `TaskFlowDemo`, não apliquei migrations e não alterei dados.

## Gestão ágil

| Requisito | Evidência documental encontrada | Avaliação |
|---|---|---|
| Backlog priorizado | `docs/backlog.md` contém US01–US13, prioridades, pontos relativos, rastreabilidade RF/RNF, critérios de aceite e sprint planejada. | Atendido como artefato de planejamento; pontos não são horas executadas. Alguns estados no cabeçalho ficaram desatualizados após Desktop/DevOps. |
| Sprints e entregas | S1 (09–13/11), S2 (14–18/11), S3 (19–22/11) descritas em backlog/cronograma; diário registra entregas antecipadas, testes e dependências. | Planejamento e acompanhamento documentados. Cronograma é plano interno, não prazo oficial da UNIP. |
| Revisão/retrospectiva | Backlog registra retrospectiva objetiva da Sprint 1 em 10/11 com fatos, dificuldades e ação para S2. | Evidência existe para Sprint 1 conforme registro da equipe; não há retrospectiva documentada de S2 ou S3 nos arquivos inspecionados. Não inferi cerimônias a partir de entregas. |
| Impedimentos/prioridade | Diário registra limitações de SQL/SSPI, credenciais manuais, AVD/espaço, testes não executados e reordenação/antecipação de atividades; backlog/cronograma registram prioridade e dependências. | Parcialmente atendido. Atualizar estado corrente e responsáveis quando a equipe confirmar. |
| Melhoria contínua | A retrospectiva S1 contém dificuldades e ação seguinte; registros do diário mostram correções resultantes de testes/auditorias. | Há evidência escrita de melhoria técnica e retrospectiva S1. Não há registro verificável de revisão/retro posterior. |

### Registro para retrospectivas ainda não documentadas

Preencher apenas após a reunião real. Este modelo não afirma que as reuniões ocorreram.

#### Sprint 2

- Data e participantes:
- Incremento demonstrado:
- O que funcionou (fatos observados):
- Dificuldades/impedimentos:
- Decisões/prioridades acordadas:
- Ações, responsáveis e prazo:

#### Sprint 3

- Data e participantes:
- Incremento demonstrado:
- O que funcionou (fatos observados):
- Dificuldades/impedimentos:
- Decisões/prioridades acordadas:
- Ações, responsáveis e prazo:

## Rastreabilidade entre disciplinas, código e evidências

| Disciplina | Requisito do manual conforme informado | Código/documentos | Evidência disponível | Lacuna real |
|---|---|---|---|---|
| Programação de Banco de Dados | Procedures, functions, triggers, transações, otimização, segurança e concorrência. | `src/TaskFlow.Infrastructure/Data`, migrations EF Core, `docs/modelo-dados.md`. | Testes SQL Server históricos de procedure/function/trigger, rollback, RowVersion e paginação; suíte 123/123 documentada em 13/11. | Não repetidos nesta auditoria por fixture destrutiva; sem benchmark/plano de consulta nem evidência de grants/segurança operacional de DB. |
| Gerenciamento de Projetos Ágil | Backlog, sprints, acompanhamento, Scrum/Kanban/Lean e melhoria contínua. | `docs/backlog.md`, `docs/cronograma.md`, `docs/diario-desenvolvimento.md`. | Histórias com critérios, três sprints planejadas, revisão/retrospectiva S1, diário com progresso/impedimentos. | Registrar retrospectivas/revisões reais S2/S3 e atualizar estados atuais sem inventar participantes ou datas. |
| Programação Aplicada em .NET | Arquitetura modular, OOP, testes/manutenção e cliente Desktop C#. | `src/TaskFlow.Domain`, `Application`, `Infrastructure`, `Api`, `Desktop`; `TaskFlow.sln`. | Builds recentes documentados; 7 testes HTTP isolados Desktop passaram na implementação. Testes SQL históricos cobrem persistência. | Demonstração Desktop autenticada real permanece pendente; não repetida nesta auditoria. |
| Cloud Computing e DevOps | Cloud, Docker, Kubernetes, CI/CD, segurança, observabilidade e escala. | `.github/workflows/ci.yml`, `infra/docker`, `infra/k8s`, `infra/README.md`, rotas `/health` e `/ready`. | Restore/build .NET e 65 testes não SQL da tarefa anterior; workflow/manifests documentados como arquivos preparados. | Docker/CI remoto/cluster/cloud não foram executados; não declarar deploy, escalabilidade operacional ou pipeline verde. |
| Integração Web | Cliente web integrado, autorização e UX. | `src/TaskFlow.Web`, `docs/diario-desenvolvimento.md`. | Testes MVC e inspeção visual histórica, incluindo preview isolado. | Fluxo autenticado com conta real não foi demonstrado nos registros consultados. |
| Integração Mobile | Flutter integrado à API, login/consulta/status. | `mobile/`, diário/README mobile. | O responsável declarou o aplicativo Mobile manualmente testado e validado; preservar essa validação como evidência vigente. | Nenhuma pendência de build/emulador foi aberta nesta auditoria. Só reabrir se surgir erro concreto. |

## Validação realizada nesta auditoria

- Leitura estática de migrations, configurações EF, repositories, unit of work, endpoints e testes SQL.
- Leitura dos artefatos de backlog, cronograma, diário e documentação de escopo/arquitetura/modelo.
- `dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --no-restore --filter "Category=Domain"`: **27 aprovados**. Não acessa SQL Server e não valida a persistência.
- Testes SQL/integração não foram executados: usam fixture temporária que cria e remove uma base, proibida pelo escopo desta tarefa. Resultados citados são somente históricos.
- Nenhuma operação SQL foi realizada; nenhum banco, migration, procedure, function, trigger, tarefa, usuário ou histórico foi alterado.

## Parecer

Há material suficiente para começar a redação acadêmica com citações a código e resultados históricos, desde que cada resultado seja apresentado com data e contexto. Ainda não há evidência suficiente para afirmar alto volume, implantação cloud operacional, pipeline remoto verde ou retrospectivas posteriores à S1. Confirmar esses itens com a equipe e anexar evidências reais antes de apresentá-los como concluídos.
