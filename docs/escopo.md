# Escopo e requisitos — TaskFlow

Referência de planejamento: **09/11/2026**. Empresa fictícia: **Alunos Tech**. Projeto acadêmico PIM VIII da UNIP. O intervalo de 09 a 22/11/2026 é o plano de trabalho da equipe; a data oficial de entrega depende da instituição.

## Problema e objetivos

Tarefas internas distribuídas em mensagens e planilhas dificultam identificar responsáveis, acompanhar prazos e recuperar decisões. O TaskFlow propõe centralizar tarefas, situação e histórico em uma API compartilhada pelos clientes web, mobile e desktop.

O MVP deverá permitir que administradores distribuam trabalho e que funcionários acompanhem e atualizem o que lhes foi atribuído. A implementação também deverá demonstrar aplicação de .NET, integridade e programação de banco de dados, execução em nuvem e gestão ágil com evidências verificáveis.

## Limites da entrega

**Dia 1:** solução .NET 8 em quatro camadas, entidades e validações fundamentais, API com `GET /health` e `GET /api/info`, testes básicos, schema SQL de referência e documentação. Resultados executados são registrados no diário e no diagnóstico do ambiente.

**MVP das duas semanas, ainda planejado:** persistência SQL Server com uma estratégia oficial de schema, autenticação e autorização reais, operações de tarefas e usuários, histórico transacional, concorrência otimista, web responsiva, clientes Flutter e desktop mínimos, procedures/functions/triggers/transações, Docker, Kubernetes e CI/CD.

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

| ID | Requisito e critério de aceite | Disciplina | Situação no Dia 1 |
| --- | --- | --- | --- |
| RF01 | Autenticar usuários e aplicar os perfis Administrador e Funcionario. Credenciais inválidas devem ser rejeitadas; rotas protegidas devem devolver 401/403 conforme o caso. | NET, BD | Perfis modelados; autenticação e autorização planejadas |
| RF02 | Permitir que somente administradores cadastrem, consultem e alterem usuários. Nome e e-mail devem ser válidos; e-mail duplicado deve ser rejeitado; senhas devem ser transformadas em hash antes da persistência. | NET, BD | Entidade e unicidade SQL de referência; gestão planejada |
| RF03 | Permitir ao administrador cadastrar e atribuir tarefas com título de até 200 caracteres, descrição opcional, prioridade válida, prazo UTC opcional e criador identificado. Nova tarefa deve iniciar Pendente. | NET, BD | Modelo e validações; caso de uso planejado |
| RF04 | Consultar tarefas por status e responsável, com paginação e visibilidade conforme a matriz de permissões. Uma consulta não pode expor tarefas de outros funcionários sem vínculo permitido. | NET, BD | Planejado |
| RF05 | Permitir alterações conforme o perfil. Status deve ser Pendente, EmAndamento ou Concluida; prioridade deve ser Baixa, Media ou Alta. Uma versão desatualizada deve produzir conflito sem sobrescrever a versão atual. | NET, BD | Enums e validações; fluxo persistido e conflito HTTP planejados |
| RF06 | Registrar criação e alterações relevantes de tarefas, identificando autor, ação, data UTC e descrição. A mudança e o registro devem ser confirmados na mesma transação; o histórico não pode ser editado pelo cliente. | NET, BD | Modelo do histórico; persistência transacional planejada |
| RF07 | Disponibilizar web responsiva e clientes mínimos Flutter e desktop C#, consumindo a mesma API. Cada cliente deve permitir autenticação, consulta das tarefas permitidas e mudança de status de uma tarefa atribuída. | NET, CLOUD | Diretórios reservados; clientes planejados |
| RF08 | Expor `GET /health` com status e timestamp UTC e `GET /api/info` com nome, versão e descrição. Ambos devem responder HTTP 200 sem exigir banco no Dia 1. | NET, CLOUD | API mínima do Dia 1 |

## Requisitos não funcionais

| ID | Requisito e critério de verificação | Disciplina | Situação no Dia 1 |
| --- | --- | --- | --- |
| RNF01 | Manter Domain independente, Application dependente de Domain, Infrastructure dependente de Application/Domain e Api dependente de Application/Infrastructure. A solução deve compilar com nullable habilitado. | NET | Base arquitetural do Dia 1 |
| RNF02 | Não versionar segredos nem armazenar senhas em texto puro. Antes de disponibilizar rotas protegidas, implementar hashing apropriado, validação de credenciais e autorização no servidor; usar HTTPS em implantação. | NET, CLOUD | Política e campo de hash; autenticação futura |
| RNF03 | Usar SQL Server com PKs, FKs, e-mail único e constraints de domínio. A estratégia de schema deve ser única; alterações de tarefa e histórico devem ter rollback conjunto em falha. | BD | Script de referência; execução e transações futuras |
| RNF04 | Tratar datas de negócio em UTC e usar `rowversion` para concorrência otimista. Validar datas no domínio e testar dois escritores sobre a mesma tarefa na etapa de persistência. | NET, BD | Datas e versão modeladas; concorrência persistida futura |
| RNF05 | Tornar a web utilizável a partir de 360 px e em desktop, com rótulos nos campos e navegação por teclado. Os clientes devem apresentar erros da API e não simular sucesso. | NET | Planejado |
| RNF06 | Executar restore, build e testes de validação, contratos HTTP e, após persistência, integração SQL. Registrar falhas e limitações reais; nenhuma alteração deve integrar o pipeline com verificações obrigatórias reprovadas. | NET, CLOUD | Verificações do Dia 1 registradas separadamente |
| RNF07 | Preparar Docker, manifests Kubernetes mínimos e CI/CD com segredos externos. Após implementação, comprovar build da imagem, probes e execução do pipeline sem credenciais no código. | CLOUD | Diretórios reservados; automação planejada |
| RNF08 | Manter histórias priorizadas e rastreáveis a RF/RNF, critérios de aceite, três sprints e diário de evidências. Revisar escopo e pendências ao final de cada sprint. | AGIL | Documentação de planejamento do Dia 1 |

## Matriz de permissões planejada

Esta matriz orientará os casos de uso e testes de autorização. Não há autenticação nem endpoints de tarefas/usuários implementados no Dia 1.

| Operação | Administrador | Funcionario | Sem autenticação |
| --- | --- | --- | --- |
| Consultar `/health` e `/api/info` | Permitido | Permitido | Permitido |
| Cadastrar, listar e alterar usuários e perfis | Permitido | Negado | Negado |
| Cadastrar e atribuir tarefa | Permitido; criador é o usuário autenticado | Negado | Negado |
| Listar/ver tarefa e seu histórico | Todas as tarefas | Somente se criador ou responsável atual | Negado |
| Alterar título, descrição, prioridade, prazo ou responsável | Qualquer tarefa | Negado | Negado |
| Alterar status | Qualquer tarefa | Somente se responsável atual | Negado |
| Fornecer autor/criador arbitrário em uma alteração | Negado; identidade vem da autenticação | Negado | Negado |
| Alterar ou remover histórico | Negado | Negado | Negado |
| Excluir definitivamente tarefa ou usuário | Fora do MVP; preservar referências e auditoria | Negado | Negado |

Reatribuição remove do antigo responsável o acesso por responsabilidade; o vínculo de criador continua válido. Funcionário que apenas criou uma tarefa pode consultá-la, mas não alterar seu status se não for o responsável atual. No MVP, a criação é administrativa; a regra de criador também mantém coerência para usuários cujo perfil mude posteriormente. Administradores podem reabrir tarefas; responsáveis podem transitar entre os três status válidos, sempre com registro de alteração efetiva.

Nenhum cliente terá acesso direto ao banco. A API deverá verificar permissões em cada operação; ocultar botões na interface não substitui essa validação. Unicidade de e-mail depende da persistência e da constraint SQL, não apenas de uma consulta prévia. Hashes nunca devem compor respostas públicas ou registros de auditoria.

## Referências internas

- [Arquitetura](arquitetura.md), [modelo de dados](modelo-dados.md) e [ambiente](ambiente.md).
- [Backlog](backlog.md), [cronograma](cronograma.md) e [diário de desenvolvimento](diario-desenvolvimento.md).
