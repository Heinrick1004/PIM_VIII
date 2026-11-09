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

## Plano das três sprints

| Sprint | Período planejado | Objetivo | Backlog selecionado | Revisão prevista |
| --- | --- | --- | --- | --- |
| S1 | 09–13/11/2026 | Base, persistência e primeiro fluxo autenticado de cadastro | US01–US05 | API com autenticação e criação persistida de tarefas, conforme capacidade real |
| S2 | 14–18/11/2026 | Consulta, atualização, histórico e uso pela web | US06–US09 | Fluxo web integrado, concorrência e evidências SQL |
| S3 | 19–22/11/2026 | Clientes mínimos, execução em nuvem e fechamento | US10–US13 | Demonstração integrada, documentação e pendências explícitas |

US01 é o escopo desta execução do Dia 1. A atribuição de uma história à sprint não indica conclusão. O quadro deverá registrar estados `A fazer`, `Em andamento`, `Em revisão` e `Concluído`, com responsável e evidência de cada item. O diário registra o que foi efetivamente entregue.

## Definição de pronto

Uma história estará pronta quando seus critérios forem demonstrados, o código respeitar a arquitetura, as verificações pertinentes passarem, a documentação estiver coerente e a revisão não encontrar segredos ou sucesso simulado. Histórias com dependência externa não verificada permanecerão pendentes, com impedimento e próximo passo registrados.

Em cada dia, revisar progresso, impedimentos e capacidade. Ao final de cada sprint, demonstrar o incremento e realizar uma retrospectiva curta. Se houver atraso, reduzir acabamento e abrangência das interfaces antes de comprometer segurança, integridade, verificação ou a veracidade das evidências; renegociar qualquer retirada de requisito com a equipe/orientação.

Dependências principais: US02 sustenta persistência de US04–US07; US03 precede exposição dos casos de negócio; US06–US07 sustentam US08, US10 e US11; US09 depende da decisão de auditoria; US12 exige verificações estáveis. Detalhes diários estão no [cronograma](cronograma.md).
