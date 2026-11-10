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
| US06 | Como funcionário, quero consultar minhas tarefas para acompanhar prioridades e prazos. | P1 / 3 | RF04, RNF02 | Listagem paginada com filtros de status/responsável; funcionário vê apenas tarefas em que é criador ou responsável; administrador vê todas; tentativas de acesso sem vínculo são bloqueadas. | S2 |
| US07 | Como responsável, quero atualizar o status da minha tarefa com histórico e proteção contra sobrescrita. | P1 / 5 | RF05, RF06, RNF03, RNF04 | Matriz de permissões aplicada; alteração efetiva gera histórico; campos administrativos protegidos; versão antiga gera 409; falha no histórico desfaz a mudança; nenhum histórico duplicado. | S2 |
| US08 | Como usuário, quero consultar tarefas em uma interface web responsiva. | P1 / 5 | RF07, RNF05 | Login, lista/detalhe e mudança de status integrados à API; usabilidade em 360 px e desktop; rótulos e teclado; erros reais apresentados ao usuário. | S2 |
| US09 | Como avaliador, quero evidências de programação de banco para verificar procedures, functions, triggers e transações. | P2 / 5 | RF04, RF06, RNF03 | Implementar objetos SQL com finalidade definida, parâmetros e testes; procedure de consulta e function com uso demonstrado; trigger sem duplicar auditoria; cenário de commit/rollback documentado. | S2 |
| US10 | Como funcionário, quero consultar e atualizar minhas tarefas pelo aplicativo Flutter mínimo. | P2 / 3 | RF07, RNF02 | Cliente consome a API; login, consulta e mudança de status; lida com falha de rede e autorização; não inclui acesso direto ao banco nem credenciais fixas. | S3 |
| US11 | Como funcionário, quero acessar as tarefas por um cliente desktop C# mínimo. | P2 / 3 | RF07, RNF02 | Cliente consome a API; login, consulta e mudança de status; apresenta erros e conflitos; não duplica autorização nem acessa SQL diretamente. | S3 |
| US12 | Como mantenedor, quero executar a API em contêiner e validar a entrega por pipeline. | P2 / 5 | RF08, RNF06, RNF07 | Dockerfile funcional; imagem compilada; manifests Kubernetes mínimos e probes coerentes; pipeline executa restore/build/testes; implantação e configuração externa documentadas com evidências reais. | S3 |
| US13 | Como equipe, queremos fechar a entrega com rastreabilidade e demonstração verificável. | P1 / 3 | RNF06, RNF08 | Revisar RF/RNF e histórias; executar testes integrados; registrar pendências conhecidas e evidências das quatro disciplinas; revisar instruções de execução; demonstrar fluxo entre clientes/API/SQL. | S3 |

## Estado executado até o Dia 5

- **Concluídas após revisão da Sprint 1:** US01 (base), US02 (SQL Server/migrations), US03 (JWT e autorização), US04 (gerenciamento de usuários) e US05 (criação/atribuição de tarefa com histórico transacional). Critérios e evidências estão na revisão abaixo.
- **Pendência de operação:** o bootstrap no banco `TaskFlowDev` e a demonstração com uma senha definida pelo mantenedor não foram executados. Não foram inventadas credenciais nem criados usuários persistentes em `TaskFlowDev`.
- **Parciais ou planejadas:** US06 tem consulta de detalhe e histórico paginado, mas ainda não possui listagem geral/filtros; US07 (atualizações), US08–US13 e as demais integrações continuam pendentes.

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
- **Ação para Sprint 2:** priorizar listagem paginada/filtros e atualizações com RowVersion e histórico. A consulta de detalhe e o histórico paginado já foram antecipados; revisar capacidade antes de iniciar interfaces e objetos SQL planejados.

## Plano das três sprints

| Sprint | Período planejado | Objetivo | Backlog selecionado | Revisão prevista |
| --- | --- | --- | --- | --- |
| S1 | 09–13/11/2026 (fechada em 10/11) | Base, persistência e primeiro fluxo autenticado de cadastro | US01–US05 | Critérios revistos; testes SQL/HTTP passaram; demonstração manual pendente |
| S2 | 14–18/11/2026 | Listagem/filtros, atualização concorrente, uso pela web e objetos SQL conforme capacidade | US06–US09 | Priorizar APIs e concorrência antes de assumir entrega de interface/objetos SQL |
| S3 | 19–22/11/2026 | Clientes mínimos, execução em nuvem e fechamento | US10–US13 | Demonstração integrada, documentação e pendências explícitas |

US01 é o escopo desta execução do Dia 1. A atribuição de uma história à sprint não indica conclusão. O quadro deverá registrar estados `A fazer`, `Em andamento`, `Em revisão` e `Concluído`, com responsável e evidência de cada item. O diário registra o que foi efetivamente entregue.

## Definição de pronto

Uma história estará pronta quando seus critérios forem demonstrados, o código respeitar a arquitetura, as verificações pertinentes passarem, a documentação estiver coerente e a revisão não encontrar segredos ou sucesso simulado. Histórias com dependência externa não verificada permanecerão pendentes, com impedimento e próximo passo registrados.

Em cada dia, revisar progresso, impedimentos e capacidade. Ao final de cada sprint, demonstrar o incremento e realizar uma retrospectiva curta. Se houver atraso, reduzir acabamento e abrangência das interfaces antes de comprometer segurança, integridade, verificação ou a veracidade das evidências; renegociar qualquer retirada de requisito com a equipe/orientação.

Dependências principais: US02 sustenta US04–US07; US03 está implementada e precede a exposição dos próximos casos de negócio; US06–US07 sustentam US08, US10 e US11; US09 depende da decisão de auditoria; US12 exige verificações estáveis. Detalhes diários estão no [cronograma](cronograma.md) e as execuções verificadas no [diário](diario-desenvolvimento.md).
