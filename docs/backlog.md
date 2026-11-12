# Product backlog — TaskFlow

Plano inicial em 09/11/2026. A prioridade usa P0 (necessário para iniciar com segurança), P1 (núcleo do MVP) e P2 (integração acadêmica e entrega). As estimativas em pontos são relativas, iniciais e devem ser revistas com a capacidade real da equipe. Não representam horas executadas.

## Histórias e critérios de aceite

| ID | História de usuário | Prioridade / pontos | Rastreabilidade | Critérios de aceite | Sprint |
| --- | --- | --- | --- | --- | --- |
| US01 | Como desenvolvedor, quero uma base organizada para evoluir o sistema com verificações reproduzíveis. | P0 / 3 | RF08, RNF01, RNF06, RNF08 | Solução com dependências corretas; entidades com validações; `/health` e `/api/info` reais; build/testes e diagnóstico registrados; documentação do Dia 1 presente. | S1, Dia 1 |
| US02 | Como desenvolvedor, quero persistência SQL Server para manter usuários, tarefas e histórico com integridade. | P0 / 5 | RF02, RF03, RF06, RNF03, RNF04 | Uma estratégia oficial de schema; mapeamentos e migration revisados; teste em banco de desenvolvimento real; PK/FK, e-mail único e rowversion verificados; nenhum segredo versionado. | S1 |
| US03 | Como usuário, quero me autenticar para acessar somente operações do meu perfil. | P0 / 5 | RF01, RNF02 | Hash apropriado com salt; login inválido rejeitado; identidade autenticada no servidor; testes de 401 e 403; hash ausente das respostas e logs. | S1 |
| US04 | Como administrador, quero gerenciar usuários para distribuir o trabalho entre pessoas cadastradas. | P1 / 3 | RF02, RNF02, RNF03 | Somente administrador cadastra/lista/altera; e-mail duplicado tratado; perfil validado; exclusão definitiva fora do MVP; nenhuma senha em texto puro. | S1 |
| US05 | Como administrador, quero cadastrar e atribuir tarefas para organizar as demandas internas. | P1 / 5 | RF03, RF05, RF06 | Criação inicia Pendente; título, prioridade e usuários validados; prazo opcional em UTC; autor vem da identidade; criação e histórico confirmados na mesma transação. | S1 |
| US06 | Como funcionário, quero consultar minhas tarefas para acompanhar prioridades e prazos. | P1 / 3 | RF04, RNF02 | Listagem paginada com filtros combináveis de status, prioridade, responsável, título e sem responsável; funcionário vê apenas tarefas em que é criador ou responsável; administrador vê todas; filtros, contagem e paginação no SQL; ordenação estável. | S2 |
| US07 | Como responsável, quero atualizar o status da minha tarefa, e como administrador quero editar seus campos administrativos, com histórico e proteção contra sobrescrita. | P1 / 5 | RF05, RF06, RNF03, RNF04 | `PATCH` só permite status ao responsável atual ou administrador; `PUT` administrativo permite título, descrição, prioridade, prazo e reatribuição (inclusive remoção); toda escrita requer RowVersion; conflito real gera 409; histórico de mudanças efetivas é atômico, sem evento duplicado; criador e data de criação são imutáveis. | S2 |
| US08 | Como usuário, quero consultar tarefas em uma interface web responsiva. | P1 / 5 | RF07, RNF05 | Login/logout por cookie protegido; painel, lista/filtros/paginação, detalhe/histórico e status integrados à API; formulários administrativos de tarefa e usuário; experiência responsiva desde 360 px; rótulos, antiforgery e erros/409 apresentados sem sucesso simulado. | S2 |
| US09 | Como avaliador, quero evidências de programação de banco para verificar procedures, functions, triggers e transações. | P2 / 5 | RF04, RF06, RNF03 | Implementar objetos SQL com finalidade definida, parâmetros e testes; procedure de consulta e function com uso demonstrado; trigger sem duplicar auditoria; cenário de commit/rollback documentado. | S2 |
| US10 | Como funcionário, quero consultar e atualizar minhas tarefas pelo aplicativo Flutter mínimo. | P2 / 3 | RF07, RNF02 | Cliente consome a API; login, consulta e mudança de status; lida com falha de rede e autorização; não inclui acesso direto ao banco nem credenciais fixas. | S3 |
| US11 | Como funcionário, quero acessar as tarefas por um cliente desktop C# mínimo. | P2 / 3 | RF07, RNF02 | Cliente consome a API; login, consulta e mudança de status; apresenta erros e conflitos; não duplica autorização nem acessa SQL diretamente. | S3 |
| US12 | Como mantenedor, quero executar a API em contêiner e validar a entrega por pipeline. | P2 / 5 | RF08, RNF06, RNF07 | Dockerfile funcional; imagem compilada; manifests Kubernetes mínimos e probes coerentes; pipeline executa restore/build/testes; implantação e configuração externa documentadas com evidências reais. | S3 |
| US13 | Como equipe, queremos fechar a entrega com rastreabilidade e demonstração verificável. | P1 / 3 | RNF06, RNF08 | Revisar RF/RNF e histórias; executar testes integrados; registrar pendências conhecidas e evidências das quatro disciplinas; revisar instruções de execução; demonstrar fluxo entre clientes/API/SQL. | S3 |

## Estado executado até o Dia 9

- **Concluídas após revisão da Sprint 1:** US01 (base), US02 (SQL Server/migrations), US03 (JWT e autorização), US04 (gerenciamento de usuários) e US05 (criação/atribuição de tarefa com histórico transacional). Critérios e evidências estão na revisão abaixo.
- **Pendência de operação:** o bootstrap no banco `TaskFlowDev` e a demonstração com uma senha definida pelo mantenedor não foram executados. Não foram inventadas credenciais nem criados usuários persistentes em `TaskFlowDev`.
- **US06 concluída no Dia 6:** lista protegida com visibilidade por criador/responsável, filtros AND, contagem/paginação SQL, ordenação estável e testes HTTP/SQL Server isolados.
- **US07 concluída no Dia 7:** alteração de status por administrador/responsável atual, edição administrativa parcial, reatribuição e remoção, RowVersion aplicado como token original EF e validado sob corrida real no SQL Server, eventos transacionais e rollback em falha de histórico.
- **US08 em andamento no Dia 8:** interface MVC implementada com autenticação por cookie, painel, tarefas, histórico e usuários, consumindo a API por cliente HTTP tipado. Os 23 testes Web e a suíte completa (113 testes) passaram; oito rotas foram inspecionadas visualmente em cinco larguras (360–1366 px), sem overflow horizontal. Resta a demonstração com conta real; a API de pré-visualização não usou `TaskFlowDev`.
- **Redesign Dia 9 implementado:** tema escuro com variáveis CSS aplicado ao login, painel, filtros/lista de tarefas, criação/edição, detalhes/histórico e administração de usuários. Microinterações leves incluem hover/foco, cards, carregamento de formulários, menu móvel e feedback; movimento reduzido e foco de teclado são respeitados. O browser conferiu as rotas principais em 360, 390, 768, 1024, 1366 e 1920 px, sem overflow horizontal, usando uma API efêmera em memória. A demonstração de conta real permanece pendente.
- **Parciais ou planejadas:** US09–US13 e as demais integrações continuam pendentes.

### Revisão provisória da US08 — Dia 8

| Critério | Evidência verificada / pendência |
| --- | --- |
| Autenticação sem expor JWT ao navegador | Login/logout usam cookie protegido por Data Protection e antiforgery; testes Web verificam fluxo e ausência do token no HTML. Login com conta real da API ainda depende do bootstrap manual. |
| Painel, tarefas, detalhe/histórico, status e administração | Controllers e views MVC consomem os endpoints da API via cliente HTTP tipado; testes Web usam API de teste controlada e testes do cliente verificam requisições, Bearer, payloads, RowVersion e erros. |
| Permissões, conflito e erro real | A interface não permite ações administrativas a funcionários, apresenta falhas da API e trata 409 sem declarar sucesso; testes Web cobrem esses casos. Permissões definitivas continuam no servidor API. |
| Responsividade e inspeção visual | As oito rotas principais foram conferidas em navegador nos viewports 360, 390, 768, 1024 e 1366 px com API de pré-visualização; nenhuma apresentou overflow horizontal. Um problema inicial em 768 px foi corrigido ampliando o breakpoint móvel. |
| Conclusão da história | **Em andamento.** Suite completa (113 aprovados) passou; resta demonstração manual com conta configurada pelo mantenedor. API de pré-visualização não usa `TaskFlowDev` nem comprova autenticação real. |

### Revisão do redesign e da revalidação — Dia 9

| Critério | Evidência verificada / pendência |
| --- | --- |
| Tema escuro consistente | Paleta CSS reutilizável aplicada às views de login, painel, tarefas, formulário, detalhe/histórico e usuários; inputs, selects, opções, painéis e erros usam superfícies escuras. |
| Interações e acessibilidade | Hover/foco animados, cards com entrada/elevação, loading de submissão, menu lateral com Escape/foco e alertas; CSS reduz animação para `prefers-reduced-motion`. |
| Layout responsivo | Browser executou login e oito rotas autenticadas nas larguras 360, 390, 768, 1024, 1366 e 1920 px; todas responderam 200 e não apresentaram overflow horizontal. API usada nesse preview foi temporária e não persistente. |
| Perfil revalidado | Testes Web cobrem administrador autorizado, rebaixamento e bloqueio imediato de GET/POST administrativo, API indisponível sem uso do perfil antigo, JWT 401 com expiração do cookie, remoção de duplicatas e uma única chamada direta a `GET /api/auth/me`. |
| Erros e compatibilidade | Testes verificam renderização de `UsersController` via `Views/Shared/ApiError.cshtml`; antiforgery, RowVersion, conflito 409 e cliente HTTP continuam sob a suíte Web/integração. |
| Uiverse e atribuição | As páginas atuais de Uiverse consultadas serviram conteúdo promocional, sem página/autoria/licença de componente identificável. Nenhum snippet de terceiro foi copiado; essa limitação está em [creditos-interface.md](creditos-interface.md). |
| Demonstração com API real | **Pendente.** Não foram fornecidas credenciais escolhidas pelo mantenedor; o preview local usa dados fictícios apenas em memória. `TaskFlowDev` não foi acessado, alterado ou usado em testes. |

### Revisão da US06 — Dia 6

| Critério | Evidência verificada |
| --- | --- |
| Administrador lista todas as tarefas; funcionário vê somente tarefa criada ou atribuída a si | `TaskList_AppliesVisibilityFiltersAndPaginationInSqlServer` executa HTTP/JWT e lê dados de SQL Server temporário; inclui usuário criador e responsável simultaneamente sem duplicar item. |
| Filtros de status, prioridade, responsável, título e sem responsável se combinam | Mesmo teste confirma filtros individuais e combinação AND; tentativa de usar responsável alheio não amplia o escopo do funcionário. |
| Contagem, paginação e ordenação são estáveis e executadas em SQL | Teste confirma contagem total, páginas sem sobreposição, página além do total vazia e desempate por Id em tarefas com a mesma data de criação. |
| Entradas inválidas são rejeitadas, autenticação é exigida e perfil corrente prevalece | Mesmo teste verifica 400 para limites/enums/combinação inválida, 401 anônimo; `TaskList_UsesCurrentDatabaseProfileForVisibility` verifica a revalidação de perfil. |
| Isolamento e persistência reais | Testes usam `TaskFlowIntegrationTests_<GUID>` e consultam as tarefas persistidas; não usam EF InMemory nem `TaskFlowDev`. |

### Revisão da US07 — Dia 7

| Critério | Evidência verificada |
| --- | --- |
| Status permitido apenas para administrador ou responsável atual; criador isolado/funcionário sem vínculo/anônimo recebem 403/401 | `TaskStatusUpdate_EnforcesPermissionsAndRejectsStaleVersionWithoutDuplicateHistory` atravessa JWT, middleware e SQL Server temporário. |
| Token obrigatório, Base64 válido de 8 bytes; versão antiga retorna 409 inclusive em no-op; atualização efetiva muda a versão | Testes de contrato/status verificam ausência, formato/tamanho incorreto, repetição atual sem evento e versão antiga rejeitada. |
| Conflito do mecanismo de concorrência SQL, sem sobrescrita nem histórico da requisição vencida | `TaskStatusUpdate_UsesSqlServerRowVersionForConcurrentHttpConflict` carrega uma instância da tarefa no pipeline HTTP e atualiza concorrente com outro `DbContext`; EF recebe `DbUpdateConcurrencyException`, API responde 409 e o novo estado no banco permanece sem evento parcial. |
| Edição/reatribuição administrativa e visibilidade atual | `AdminTaskUpdate_ReassignsAndRemovesAssigneeWithAtomicHistoryAndStableIdentity` testa todos os campos, novo status pelo novo responsável, perda de acesso do antigo, consulta do criador, remoção do responsável, listagem/histórico e imutabilidade de criador/data. |
| Referência de usuário e perfil de responsável | `AdminTaskUpdate_RejectsUnknownOrNonEmployeeAssignee` comprova 404 para inexistente e 400 para perfil Administrador. |
| Histórico atômico e atualizações sem efeito | `AdminTaskUpdate_RollsBackAllFieldsAndHistoryWhenHistoryWriteFails` injeta falha depois do primeiro `SaveChangesAsync`; novo contexto SQL confirma rollback de valores, rowversion e eventos. A edição idêntica preserva RowVersion e contagem de histórico. |
| Eventos não duplicados e sem cópia de texto livre | Os testes de domínio cobrem ações e no-op; descrições de auditoria são genéricas e não copiam título, descrição ou identificadores de responsável. |

## Revisão e retrospectiva da Sprint 1 — 10/11/2026

Fechamento antecipado em relação à janela planejada (09–13/11), a pedido da etapa Dia 5. As histórias US01–US05 foram revistas contra seus critérios; a demonstração manual do bootstrap não é declarada como realizada e permanece uma pendência operacional explícita.

| História | Critérios e resultado | Evidência verificada | Pendências |
| --- | --- | --- | --- |
| US01 — base e verificações | Solução em camadas, health/info, build/testes reproduzíveis e documentação existentes. **Concluída.** | Testes `ApiEndpointTests`; `dotnet restore/build/test`; árvore de projetos e documentos versionados. | Nenhuma pendência dos critérios da história. |
| US02 — persistência SQL Server | Migrations EF Core são a estratégia oficial; modelo, FKs, unicidade, UTC, RowVersion e transações foram testados em SQL Server real. **Concluída.** | `SqlServerPersistenceTests`, `SqlServerTestDatabase` e histórico das migrations documentado nos Dias 2 e 5. | Não executar bootstrap manual em `TaskFlowDev` até o mantenedor configurar credenciais. |
| US03 — autenticação/autorização | Hash apropriado, JWT validado, 401/403 e perfil corrente aplicado nas rotas. **Concluída.** | `AuthenticationEndpointTests`, `AdminBootstrapperTests`, `JwtSettingsTests`; suíte HTTP com SQL isolado. | Demonstração manual do login após configuração local do segredo e bootstrap. |
| US04 — gerenciamento de usuários | Operações administrativas, e-mail duplicado, perfis, senha e proteção do último administrador testados. **Concluída.** | `AuthenticationEndpointTests` com middleware real e persistência SQL Server isolada. | CRUD permanece sem exclusão definitiva, conforme o escopo. |
| US05 — criação/atribuição de tarefas | Tarefa Pendente, validação, autoria pelo JWT, responsável existente opcional e histórico de criação na mesma transação. **Concluída.** | `TaskEndpointTests`: fluxo HTTP, persistência, evento único, RowVersion e teste de rollback; Dia 5 acrescenta consulta protegida do histórico. | Demonstração manual em Swagger/`TaskFlowDev`; nenhuma credencial permanente foi criada. |

### Retrospectiva objetiva

- **Funcionou:** manter o SQL Server real como critério de persistência; os fixtures temporários isolados permitiram exercitar middleware, FKs, transação e autorização sem escrever em `TaskFlowDev`. Repositórios e unidade de trabalho existentes foram reutilizados sem introduzir outra camada.
- **Dificuldades:** o mapeamento inicial de RowVersion e salvamentos individuais por repositório exigiram correções na etapa de persistência; a validação integrada real ajudou a detectar essas falhas. A demonstração manual continua bloqueada pela ausência de credenciais escolhidas pelo mantenedor.
- **Decisões mantidas:** migrations são a única evolução oficial do schema; histórico é gravado pela aplicação na transação, sem trigger duplicador; testes de escrita usam banco SQL Server efêmero; nenhum bootstrap automático.
- **Ação para Sprint 2:** manter US08 em andamento até a demonstração operacional com conta real; selecionar/planejar US09 (objetos SQL) conforme capacidade. US06 e US07 foram concluídas; a Sprint 2 permanece aberta.

## Plano das três sprints

| Sprint | Período planejado | Objetivo | Backlog selecionado | Revisão prevista |
| --- | --- | --- | --- | --- |
| S1 | 09–13/11/2026 (fechada em 10/11) | Base, persistência e primeiro fluxo autenticado de cadastro | US01–US05 | Critérios revistos; testes SQL/HTTP passaram; demonstração manual pendente |
| S2 | 14–18/11/2026 | Atualização concorrente, uso pela web e objetos SQL conforme capacidade | US07–US09 | US06 concluída; priorizar API/concorrência antes de assumir interface/objetos SQL |
| S3 | 19–22/11/2026 | Clientes mínimos, execução em nuvem e fechamento | US10–US13 | Demonstração integrada, documentação e pendências explícitas |

US01 é o escopo desta execução do Dia 1. A atribuição de uma história à sprint não indica conclusão. O quadro deverá registrar estados `A fazer`, `Em andamento`, `Em revisão` e `Concluído`, com responsável e evidência de cada item. O diário registra o que foi efetivamente entregue.

## Definição de pronto

Uma história estará pronta quando seus critérios forem demonstrados, o código respeitar a arquitetura, as verificações pertinentes passarem, a documentação estiver coerente e a revisão não encontrar segredos ou sucesso simulado. Histórias com dependência externa não verificada permanecerão pendentes, com impedimento e próximo passo registrados.

Em cada dia, revisar progresso, impedimentos e capacidade. Ao final de cada sprint, demonstrar o incremento e realizar uma retrospectiva curta. Se houver atraso, reduzir acabamento e abrangência das interfaces antes de comprometer segurança, integridade, verificação ou a veracidade das evidências; renegociar qualquer retirada de requisito com a equipe/orientação.

Dependências principais: US02 sustenta US04–US07; US03 está implementada e precede a exposição dos próximos casos de negócio; US06 está concluída e US07 sustenta US08, US10 e US11; US09 depende da decisão de auditoria; US12 exige verificações estáveis. Detalhes diários estão no [cronograma](cronograma.md) e as execuções verificadas no [diário](diario-desenvolvimento.md).
