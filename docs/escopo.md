# Escopo e requisitos — TaskFlow

Referência de planejamento: **09/11/2026**. Empresa fictícia: **Alunos Tech**. Projeto acadêmico PIM VIII da UNIP. O intervalo de 09 a 22/11/2026 é o plano de trabalho da equipe; a data oficial de entrega depende da instituição.

## Problema e objetivos

Tarefas internas distribuídas em mensagens e planilhas dificultam identificar responsáveis, acompanhar prazos e recuperar decisões. O TaskFlow propõe centralizar tarefas, situação e histórico em uma API compartilhada pelos clientes web, mobile e desktop.

O MVP deverá permitir que administradores distribuam trabalho e que funcionários acompanhem e atualizem o que lhes foi atribuído. A implementação também deverá demonstrar aplicação de .NET, integridade e programação de banco de dados, execução em nuvem e gestão ágil com evidências verificáveis.

## Limites da entrega

**Dia 1:** solução .NET 8 em quatro camadas, entidades e validações fundamentais, API com `GET /health` e `GET /api/info`, testes básicos, schema SQL de referência e documentação. Resultados executados são registrados no diário e no diagnóstico do ambiente.

**Estado após Dia 7:** persistência SQL Server com migrations, autenticação JWT, gerenciamento administrativo de usuários, criação e listagem de tarefas, atualização de status/edição administrativa com RowVersion e histórico transacional estão implementados e testados em SQL Server isolado. US01–US07 estão concluídas segundo critérios automatizados; a Sprint 2 continua em andamento. A demonstração manual em `TaskFlowDev` segue pendente. Clientes, procedures/functions/triggers, Docker, Kubernetes e CI/CD continuam planejados.

Ficam fora do MVP: microserviços, CQRS, MediatR, múltiplas empresas, chat, anexos, notificações externas, recuperação de senha por e-mail, modo offline e relatórios analíticos avançados. Não haverá endpoints de sucesso para operações ainda não implementadas.

## Rastreabilidade às disciplinas

| Sigla | Disciplina | Evidência planejada |
| --- | --- | --- |
| NET | Programação Aplicada em .NET | Entidades, API, DI, validações, testes e cliente desktop C# |
| BD | Programação de Banco de Dados | Modelo relacional, constraints, consultas, concorrência e programação SQL |
| CLOUD | Cloud Computing e DevOps | Contêiner, implantação, configuração externa e pipeline |
| AGIL | Gerenciamento de Projetos Ágil | Backlog priorizado, sprints, critérios de aceite, revisões e diário |

## Requisitos funcionais

Os critérios abaixo descrevem o comportamento esperado do MVP. A coluna de situação diferencia a base do Dia 1 de funcionalidades futuras.

| ID | Requisito e critério de aceite | Disciplina | Situação verificada |
| --- | --- | --- | --- |
| RF01 | Autenticar usuários e aplicar os perfis Administrador e Funcionario. Credenciais inválidas devem ser rejeitadas; rotas protegidas devem devolver 401/403 conforme o caso. | NET, BD | Implementado e testado com middleware JWT real; perfil revalidado no banco |
| RF02 | Permitir que somente administradores cadastrem, consultem e alterem usuários. Nome e e-mail devem ser válidos; e-mail duplicado deve ser rejeitado; senhas devem ser transformadas em hash antes da persistência. | NET, BD | Endpoints implementados; senha hasheada; CRUD sem exclusão; SQL isolado testado |
| RF03 | Permitir ao administrador cadastrar e atribuir tarefas com título de até 200 caracteres, descrição opcional, prioridade válida, prazo UTC opcional e criador identificado. Nova tarefa deve iniciar Pendente. | NET, BD | `POST /api/tasks` implementado e validado em SQL Server; histórico criado na mesma transação |
| RF04 | Consultar tarefas por status e responsável, com paginação e visibilidade conforme a matriz de permissões. Uma consulta não pode expor tarefas de outros funcionários sem vínculo permitido. | NET, BD | Implementado: lista SQL paginada com status, prioridade, responsável, ausência de responsável e título; escopo aplicado antes de filtros/contagem/página |
| RF05 | Permitir alterações conforme o perfil. Status deve ser Pendente, EmAndamento ou Concluida; prioridade deve ser Baixa, Media ou Alta. Uma versão desatualizada deve produzir conflito sem sobrescrever a versão atual. | NET, BD | `PATCH /api/tasks/{id}/status` e `PUT /api/tasks/{id}` exigem RowVersion de 8 bytes; conflito SQL real retorna 409; validado por teste HTTP e dois DbContexts |
| RF06 | Registrar criação e alterações relevantes de tarefas, identificando autor, ação, data UTC e descrição. A mudança e o registro devem ser confirmados na mesma transação; o histórico não pode ser editado pelo cliente. | NET, BD | Criação e atualizações efetivas registram eventos atômicos; rollback real confirmado em outro DbContext; consulta continua paginada e somente leitura |
| RF07 | Disponibilizar web responsiva e clientes mínimos Flutter e desktop C#, consumindo a mesma API. Cada cliente deve permitir autenticação, consulta das tarefas permitidas e mudança de status de uma tarefa atribuída. | NET, CLOUD | Diretórios reservados; clientes planejados |
| RF08 | Expor `GET /health` com status e timestamp UTC e `GET /api/info` com nome, versão e descrição. Ambos devem responder HTTP 200 sem exigir banco no Dia 1. | NET, CLOUD | API mínima do Dia 1 |

## Requisitos não funcionais

| ID | Requisito e critério de verificação | Disciplina | Situação verificada |
| --- | --- | --- | --- |
| RNF01 | Manter Domain independente, Application dependente de Domain, Infrastructure dependente de Application/Domain e Api dependente de Application/Infrastructure. A solução deve compilar com nullable habilitado. | NET | Arquitetura mantida e solução .NET 10 compilada |
| RNF02 | Não versionar segredos nem armazenar senhas em texto puro. Antes de disponibilizar rotas protegidas, implementar hashing apropriado, validação de credenciais e autorização no servidor; usar HTTPS em implantação. | NET, CLOUD | Segredo externo; hashing e autorização implementados; HTTPS de implantação pendente |
| RNF03 | Usar SQL Server com PKs, FKs, e-mail único e constraints de domínio. A estratégia de schema deve ser única; alterações de tarefa e histórico devem ter rollback conjunto em falha. | BD | Migrations EF Core e constraints implantadas; transações testadas em SQL Server |
| RNF04 | Tratar datas de negócio em UTC e usar `rowversion` para concorrência otimista. Validar datas no domínio e testar dois escritores sobre a mesma tarefa na etapa de persistência. | NET, BD | UTC/RowVersion e concorrência otimista testados em SQL Server |
| RNF05 | Tornar a web utilizável a partir de 360 px e em desktop, com rótulos nos campos e navegação por teclado. Os clientes devem apresentar erros da API e não simular sucesso. | NET | Planejado |
| RNF06 | Executar restore, build e testes de validação, contratos HTTP e, após persistência, integração SQL. Registrar falhas e limitações reais; nenhuma alteração deve integrar o pipeline com verificações obrigatórias reprovadas. | NET, CLOUD | Restore/build/testes de domínio, HTTP e SQL Server executados e registrados |
| RNF07 | Preparar Docker, manifests Kubernetes mínimos e CI/CD com segredos externos. Após implementação, comprovar build da imagem, probes e execução do pipeline sem credenciais no código. | CLOUD | Diretórios reservados; automação planejada |
| RNF08 | Manter histórias priorizadas e rastreáveis a RF/RNF, critérios de aceite, três sprints e diário de evidências. Revisar escopo e pendências ao final de cada sprint. | AGIL | Documentação de planejamento do Dia 1 |

## Matriz de permissões vigente

As rotas implementadas aplicam esta matriz na API. Para funcionário, “vinculado” significa criador ou responsável atual no detalhe da tarefa.

| Operação | Administrador | Funcionario | Sem autenticação |
| --- | --- | --- | --- |
| Consultar `/health` e `/api/info` | Permitido | Permitido | Permitido |
| `POST /api/auth/login` | Permitido | Permitido | Permitido; credenciais inválidas recebem 401 |
| `GET /api/auth/me` | Própria identidade do token | Própria identidade do token | 401 |
| Cadastrar, listar e alterar usuários e perfis | Permitido | Negado | Negado |
| Bootstrap do primeiro administrador | Comando local explicitamente executado; apenas Development/TaskFlowDev | Não aplicável | Não aplicável |
| Cadastrar e atribuir tarefa | Permitido; criador é o usuário autenticado e responsável funcionário é opcional | Negado | 401 |
| Editar título, descrição, prioridade, prazo ou responsável (`PUT /api/tasks/{id}`) | Permitido; requer RowVersion; `null` remove descrição/prazo/responsável | 403 | 401 |
| Consultar detalhe `GET /api/tasks/{id}` | Qualquer tarefa | Somente se criador ou responsável atual | 401 |
| `GET /api/tasks` | Todas as tarefas | Somente criadas pelo usuário ou atualmente atribuídas | 401 |
| Consultar histórico `GET /api/tasks/{id}/history` | Qualquer tarefa | Somente tarefa criada ou atualmente atribuída | 401 |
| Alterar status (`PATCH /api/tasks/{id}/status`) | Qualquer tarefa | Somente se responsável atual | 401 |
| Fornecer autor/criador arbitrário em uma alteração | Negado; identidade vem da autenticação | Negado | Negado |
| Alterar ou remover histórico | Negado | Negado | Negado |
| Excluir definitivamente tarefa ou usuário | Fora do MVP; preservar referências e auditoria | Negado | Negado |

Reatribuição remove do antigo responsável o acesso por responsabilidade; o vínculo de criador continua válido. Funcionário que apenas criou uma tarefa pode consultá-la, mas não alterar seu status se não for o responsável atual. No MVP, a criação é administrativa; a regra de criador também mantém coerência para usuários cujo perfil mude posteriormente. Administradores podem reabrir tarefas; responsáveis podem transitar entre os três status válidos, sempre com registro de alteração efetiva.

Nenhum cliente terá acesso direto ao banco. A API deverá verificar permissões em cada operação; ocultar botões na interface não substitui essa validação. Unicidade de e-mail depende da persistência e da constraint SQL, não apenas de uma consulta prévia. Hashes nunca devem compor respostas públicas ou registros de auditoria.

O histórico é somente leitura pela API. `GET /api/tasks/{id}/history` pagina os registros por `DataUtc` e, em caso de empate, por `Id`; `page` começa em 1 e `pageSize` default é 20, máximo 100. A consulta é filtrada no SQL Server e usa `AsNoTracking`.

`GET /api/tasks` retorna `{ items, page, pageSize, totalCount }`, com filtros opcionais `status`, `prioridade`, `responsavelId`, `semResponsavel` e `titulo` combinados por AND. `page` começa em 1 (limite 1.000.000), `pageSize` default 20 (máximo 100); a ordenação é descendente por `CriadaEmUtc` e `Id`. O funcionário sempre recebe escopo SQL de criador ou responsável atual, independente dos filtros enviados; `semResponsavel=true` não pode ser combinado com `responsavelId`.

JWT exige issuer, audience e chave HMAC externa em Base64 com ao menos 256 bits; a expiração é de 60 minutos. O perfil é revalidado no banco em cada requisição protegida. Login possui rate limit de 10 tentativas por minuto por IP. Ver [instruções de configuração](../README.md#segredo-jwt-e-primeiro-administrador-powershell) e os resultados em [ambiente](ambiente.md).

## Referências internas

- [Arquitetura](arquitetura.md), [modelo de dados](modelo-dados.md) e [ambiente](ambiente.md).
- [Backlog](backlog.md), [cronograma](cronograma.md) e [diário de desenvolvimento](diario-desenvolvimento.md).
