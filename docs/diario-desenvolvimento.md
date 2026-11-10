# Diário de desenvolvimento

## Dia 1 — 09/11/2026: fundação e planejamento técnico

### Atividades concluídas

- Inspecionados arquivos existentes e ferramentas, sem substituir a solução ou remover o PDF local. Ausência inicial de Git identificada; repositório local inicializado em `main`, com `origin` fornecido e sem publicação remota.
- Mantidos os cinco projetos válidos em `TaskFlow.sln` e as referências Api → Application/Infrastructure, Infrastructure → Application/Domain e Application → Domain. Domain não depende das demais camadas.
- Fixada a seleção da faixa de SDK .NET 8 por `global.json`, com nullable nos projetos e warnings tratados como erros. Registrada reavaliação da versão no Dia 2 devido ao encerramento de suporte durante o cronograma.
- Revisadas entidades Usuario, Tarefa e HistoricoTarefa: textos, IDs e enums validados, datas UTC, e-mail normalizado e RowVersion opcional de 8 bytes com cópia defensiva. As mutações existentes exigem autor/data e retornam histórico de mudança efetiva; sem persistência simulada ou autenticação fictícia.
- API mínima com `/health` e `/api/info`, metadados e relógio por DI, Swagger somente em Development e arquivo `.http` atualizado. Inicialização independente de banco.
- SQL de referência revisado para as três tabelas, unicidade de e-mail, PKs/FKs, checks, defaults UTC, índice e rowversion. Criação atômica e recusas explícitas contra sobreposição de schema. **SQL não executado**; preferência por migrations como estratégia oficial futura exclusiva.
- Reservados web, desktop, mobile, Docker e Kubernetes com `.gitkeep`, sem projetos vazios na solução.
- Criados README, requisitos com rastreabilidade e permissões, diagramas, backlog, plano de três sprints, cronograma, diagnóstico e modelo de dados.
- Criados `.gitignore` e `.editorconfig`; removidas referências NuGet desnecessárias a bibliotecas já disponíveis no runtime de testes. Não foi necessário `.env.example`.

### Evidências de validação

| Verificação | Resultado |
| --- | --- |
| Restore da solução | Cinco projetos restaurados com sucesso. |
| Build com warnings como erros | Sucesso; 0 avisos, 0 erros. |
| xUnit | 30 aprovados: 25 de domínio + 5 de integração; 0 reprovados, 0 ignorados. |
| API em processo local e HTTP/TCP | `/health`, `/api/info`, JSON OpenAPI e Swagger UI retornaram 200. Timestamp UTC comparado ao relógio real; processo encerrado após teste. |
| Revisão independente de src/tests | Sem falha concreta encontrada no escopo Dia 1. Não substitui execução dos testes acima. |
| SQL Server | Conexão de diagnóstico somente leitura bem-sucedida; script de schema não aplicado. |
| Revisão final do repositório | `git diff --cached --check` aprovado; 38 arquivos pretendidos preparados; nenhum padrão comum de segredo identificado; PDF com SHA-256 preservado e artefatos gerados ignorados. |
| Coerência documental | Oito documentos revisados e 26 links locais válidos; TRX e evidência HTTP conferidos contra os resultados descritos. |

Comandos completos, versões e respostas estão em [ambiente.md](ambiente.md). TRX, JSON HTTP e logs foram gerados em `artifacts/`, ignorado pelo Git. A implementação não promete persistência ou controle de acesso a partir desses testes.

### Limitações e decisões

- Docker ausente no PATH e locais padrão; cluster, nuvem, emuladores e toolchains mobile não verificados. Não impedem o Dia 1.
- .NET 8 disponível e mantido conforme solicitado; reavaliar .NET 10 no Dia 2 antes de implantação.
- Unicidade de e-mail, FKs, transações de alteração/histórico e concorrência entre escritores ainda exigem validação no SQL Server com a futura persistência.
- A entidade recebe hash previamente calculado como valor opaco. Presença/tamanho não provam segurança criptográfica; hashing e autenticação ainda não implementados.
- O executor em sandbox apresentou erro de infraestrutura após `git init`; houve contorno por comandos revisados fora do sandbox e exceção de confiança Git limitada ao comando, sem alterar política global.

### Versionamento e critério de encerramento

O commit local solicitado é `feat: initialize TaskFlow solution and day-one documentation`. Seu SHA e o estado final são apresentados no relatório da execução e podem ser consultados com `git log -1 --oneline` e `git status --short --branch`. Nenhum push ou PR faz parte desta etapa. O `Manual.pdf` permanece local e ignorado; bin/obj, logs e resultados gerados não entram no commit.

### Próximas ações — Dia 2

1. Registrar decisão de versão .NET e repetir build/testes se houver migração.
2. Confirmar EF Core migrations como estratégia oficial exclusiva e revisar mapeamentos, inclusive UTC e `Versao` ↔ `RowVersion`.
3. Preparar banco de desenvolvimento explicitamente identificado e conexão por configuração externa, sem credenciais versionadas.
4. Aplicar a primeira migration revisada e testar e-mail duplicado, FKs, rowversion e rollback conjunto de tarefa/histórico conforme os casos de uso forem introduzidos.
5. Refinar backlog com os resultados; autenticação e endpoints de negócio permanecem na sequência planejada, sem ampliar o escopo do Dia 1.

## Dia 2 — 10/11/2026: EF Core, migrations e persistência real

### Implementação e decisões

- Confirmados novamente SDK 10.0.403 e SQL Server local 17.0.1145.4. Projetos permanecem em `net10.0`; EF Core, SQL Server provider, `DbContext`, configurações Fluent API e registro de Infrastructure na API estão ativos.
- A conexão de Development aponta explicitamente para `TaskFlowDev` por autenticação integrada. Um teste API comprovou `CanConnectAsync` nesse banco. Foi conferido o banco existente, suas migrations e contagens (zero registros nas três tabelas) antes de atualizar. Sem `EnsureCreated`, banco substituto, `DROP DATABASE` ou migração automática no startup.
- Migrations oficiais: `InitialCreate` preexistente, seguida por `HardenPersistenceMappings` e `DatabaseDefaults`. A estratégia única de schema é EF Core migrations; `database/scripts/001_schema.sql` continua referência e não foi executado.
- Corrigido o mapeamento de RowVersion: EF acessa `_rowVersion` pelo campo, persiste na coluna `Versao NOT NULL`, lê token de 8 bytes e configura concorrência otimista. SQL Server exige remover/recriar a coluna rowversion para corrigir nulabilidade; a migration preserva linhas/todos os demais dados e gera novo token. `TaskFlowDev` estava vazio quando a migration foi aplicada.
- Configurados collation CI_AS de e-mail, defaults planejados para IDs/enums/datas, conversores UTC e checks/relacionamentos sem cascade. Paginação desempata por Id; limites negativos/zero são rejeitados.
- Repositórios específicos mantidos. Contrato `IUnitOfWork` compartilha o `TaskFlowDbContext` scoped e envolve os `SaveChangesAsync` dos repositórios para commit/rollback de tarefa e histórico. Violações SQL 2601/2627 e 547 são expostas como `PersistenceConstraintException`; conflitos rowversion continuam `DbUpdateConcurrencyException`.
- Configuração ausente para connection string falha explicitamente em vez de selecionar banco por fallback.

### Integração SQL Server e evidência

Os testes criam o banco temporário **`TaskFlowIntegrationTests_<GUID>`**, aplicam todas as migrations, limpam dados somente nas próprias tabelas entre casos e removem somente o banco temporário criado pelo fixture. A última execução SQL filtrada usou `TaskFlowIntegrationTests_f05850514fb440af9e70301c85493240`; a suíte completa final usou `TaskFlowIntegrationTests_fd03a55e59f54bf096103f6a555dfd0e`. `TaskFlowDev` não é alvo dos testes de persistência. O teste API apenas confirma o nome e abre conexão real ao banco de desenvolvimento, sem escrita.

| Grupo/execução | Aprovados | Reprovados | Ignorados |
| --- | ---: | ---: | ---: |
| Domínio (`Category=Domain`) | 25 | 0 | 0 |
| HTTP/API (`Category=Http`) | 6 | 0 | 0 |
| SQL Server (`Category=SqlServer`) | 11 | 0 | 0 |
| Suíte completa (`dotnet test TaskFlow.sln`) | 42 | 0 | 0 |

O teste de concorrência carregou a mesma tarefa em dois `DbContext`s. O primeiro persistiu uma nova versão; o segundo recebeu `DbUpdateConcurrencyException`, e a verificação independente confirmou que o estado do primeiro escritor permaneceu. O teste de rollback atualizou a tarefa, tentou gravar histórico com FK inválida e fez rollback; leitura em contexto novo confirmou o status original e zero histórico. O teste de commit confirmou tarefa+histórico juntos e repetição do mesmo status não produziu segundo evento. Outro teste exerceu os defaults de Id, status, prioridade e datas diretamente no SQL Server.

### Validação do build e do ambiente

- `dotnet restore TaskFlow.sln`: sucesso.
- `dotnet build TaskFlow.sln --no-restore --nologo`: sucesso, 0 avisos, 0 erros.
- `dotnet test TaskFlow.sln`: 42 aprovados, 0 reprovados, 0 ignorados.
- As migrations corretivas foram testadas inicialmente em banco temporário e então aplicadas ao banco de desenvolvimento previamente confirmado, sem script SQL manual. Verificação posterior confirmou o histórico, collation, `rowversion NOT NULL` e tabelas.
- A suíte SQL não foi ignorada nem substituída por provider em memória. Se SQL Server/permissões estiverem indisponíveis, os testes falham explicitamente.
- `dotnet --info`, estado dos bancos, conexão API, categorização, erros corrigidos, verificação de credenciais e comandos finais estão registrados em [ambiente.md](ambiente.md).

### Escopo e pendências

- Não foram implementados login/JWT, endpoints CRUD de negócio, interfaces Web/Flutter/Desktop, Docker, Kubernetes ou CI/CD.
- A API ainda expõe somente health e informações; autenticação/autorização e casos de uso devem preceder endpoints com dados.
- Usuários devem configurar `TASKFLOW_TEST_SQLSERVER_CONNECTION_STRING` fora do repositório se não usarem `localhost` com autenticação integrada; a conta precisa criar/remover bancos temporários.

## Dia 3 — autenticação JWT e gerenciamento de usuários (implementado em 10/11/2026)

### Implementação

- Adicionado `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.0. A API exige `Authentication:Jwt:Issuer`, `Audience` e chave Base64 externa com pelo menos 256 bits; a chave ausente ou fraca faz a configuração falhar explicitamente. Em Development, `UserSecretsId` permite guardar a chave fora do repositório.
- Configurado HMAC-SHA256, validação obrigatória de issuer, audience, assinatura e expiração de 60 minutos. JWT contém somente `sub` e `role`. Em cada token validado, a API busca o usuário corrente no banco e constrói a role com o perfil atual, impedindo que privilégio antigo persista após mudança de perfil.
- Senhas usam `PasswordHasher<Usuario>` do ASP.NET Core Identity, mínimo de 12 e máximo de 128 caracteres, sem senha/hash nas respostas. Login atualiza hashes que retornem `SuccessRehashNeeded`.
- Login limitado a 10 tentativas por minuto por IP. `/health` e `/api/info` seguem públicos; `GET /api/auth/me` requer token e `/api/users` exige Administrador.
- Adicionados cadastro/listagem paginada/consulta/atualização administrativa de usuários, validação de domínio, conversão de violações únicas para 409 e bloqueio de rebaixamento do último administrador em transação serializável. Não há exclusão de usuários.
- O bootstrap é um comando explícito `--bootstrap-admin`, restrito a Development e ao catálogo `TaskFlowDev`; solicita senha interativamente sem eco, verifica administradores existentes e não aplica migrations. Testado em banco SQL isolado; não executado em `TaskFlowDev`.
- Nenhuma migration/schema foi modificada. Swagger ganhou esquema Bearer e continua habilitado somente em Development.

### Validação final real

| Verificação | Aprovados | Reprovados | Ignorados |
| --- | ---: | ---: | ---: |
| Restore `dotnet restore TaskFlow.sln` | Sucesso | — | — |
| Build `dotnet build TaskFlow.sln --no-restore` | 0 avisos | 0 erros | — |
| Domínio (`Category=Domain`) | 25 | 0 | 0 |
| HTTP/middleware (`Category=Http`) | 13 | 0 | 0 |
| SQL Server (`Category=SqlServer`) | 19 | 0 | 0 |
| Segurança (`Category=Security`) | 2 | 0 | 0 |
| Suíte completa (`dotnet test TaskFlow.sln`) | **52** | **0** | **0** |

Os grupos HTTP e SQL Server se sobrepõem nos sete novos testes de endpoints que executam com SQL Server real; os resultados categorizados, portanto, não devem ser somados. A suíte completa e cada fixture filtrado utilizaram exclusivamente bancos temporários `TaskFlowIntegrationTests_<GUID>` para testes de persistência. Os fixtures os removeram após a execução. `TaskFlowDev` foi consultado apenas em leitura depois da validação: as três migrations existentes permanecem aplicadas e as três tabelas continuam com zero registros. Sem migration, alteração ou gravação neste dia.

### Pendências e operação manual

- Configurar localmente uma chave JWT com User Secrets/ambiente e executar o bootstrap somente quando o mantenedor escolher nome/e-mail/senha. O procedimento e os comandos estão no [README](../README.md); nenhuma credencial foi inventada.
- O fluxo login → `/me` → CRUD → funcionário 403 foi exercitado por testes HTTP contra SQL isolado, mas não foi demonstrado manualmente no Swagger de `TaskFlowDev`; não havia credenciais fornecidas para isso.
- Permanecem pendentes endpoints de tarefas, histórico HTTP, interfaces web/mobile/desktop, Docker, Kubernetes e CI/CD.
- Restore, build, categorias, preservação do banco e resultados estão detalhados em [ambiente.md](ambiente.md).

## Dia 4 — criação e atribuição de tarefas (implementado em 10/11/2026)

### Implementação

- Adicionados DTOs específicos para criar tarefa e responder com os campos públicos e RowVersion Base64; a entidade EF não é serializada diretamente.
- `POST /api/tasks` exige JWT e policy Administrador. Valida corpo, título (1–200), prioridade, responsável não vazio e prazo UTC. O usuário responsável deve existir e ter perfil Funcionario; a atribuição permanece opcional. Payload sem corpo ou campos inválidos retorna 400, responsável ausente no banco retorna 404 e constraints de integridade retornam 409.
- O `CriadorId` deriva do `sub` do principal JWT revalidado no banco. Campo adicional `criadorId` enviado pelo cliente não faz parte do contrato nem substitui o autor autenticado.
- A tarefa inicia Pendente e recebe Id e hora UTC do servidor. O evento único `CriarTarefa` registra Id próprio, tarefa, administrador, instante UTC e descrição não sensível.
- Repositórios existentes salvam tarefa e histórico sob a mesma transação da `IUnitOfWork`; não foram adicionados triggers, outro mecanismo de transação, serviço de aplicação ou mudança arquitetural.
- `GET /api/tasks/{id}` permite consulta ampla a administradores e consulta a funcionário apenas se for criador ou responsável. Tarefa inexistente retorna 404; usuário sem vínculo recebe 403.
- Sem alterações de entidade, schema ou migrations. `TaskFlowDev` não foi alvo de gravação ou migration; o bootstrap não foi executado. Testes usam exclusivamente `TaskFlowIntegrationTests_<GUID>`.

### Validação final real

| Verificação | Aprovados | Reprovados | Ignorados |
| --- | ---: | ---: | ---: |
| `dotnet restore TaskFlow.sln` | Sucesso | — | — |
| `dotnet build TaskFlow.sln` | 0 avisos | 0 erros | — |
| Domínio (`Category=Domain`) | 25 | 0 | 0 |
| HTTP (`Category=Http`) | 32 | 0 | 0 |
| SQL Server (`Category=SqlServer`) | 38 | 0 | 0 |
| Segurança (`Category=Security`) | 2 | 0 | 0 |
| Suíte completa (`dotnet test TaskFlow.sln`) | **71** | **0** | **0** |

Os grupos HTTP e SQL Server se sobrepõem nos testes de endpoints que usam SQL Server isolado. Os testes demonstram persistência da tarefa e RowVersion de oito bytes, exatamente um evento de criação, commit conjunto, UTC, autoria proveniente do token, rejeição de atribuição inválida, 401/403 e autorização por vínculo. Para rollback, o repositório real salva o histórico dentro da transação e um adaptador de falha de teste lança exceção; leitura posterior em outro `DbContext` confirma zero tarefa e zero histórico. Não houve falha não intencional na suíte.

### Pendências

- A demonstração manual no Swagger contra `TaskFlowDev` requer configurar a chave JWT e credenciais escolhidas pelo mantenedor, executar explicitamente o bootstrap e validar o fluxo. Nenhuma senha foi inventada.
- Listagem geral de tarefas/filtros, edição/reatribuição, interfaces, objetos SQL programáveis, Docker, Kubernetes e CI/CD continuam fora deste incremento.
- Próxima etapa: iniciar alterações administrativas com histórico transacional e controle RowVersion, definir a matriz de mudança de campos e testar conflitos HTTP sem sobrescrita; manter novos testes em SQL Server isolado.

## Dia 5 — consulta de histórico e fechamento da Sprint 1 (implementado em 10/11/2026)

### Implementação

- Adicionado `GET /api/tasks/{id}/history`, somente leitura, atrás de autenticação. Administrador consulta qualquer tarefa; funcionário só consulta quando é criador ou responsável atual. IDs GUID vazios resultam em 400, GUID malformado não casa a rota (404), tarefa inexistente retorna 404, anônimo 401 e usuário autenticado sem vínculo 403.
- Criado DTO de evento e resposta paginada contendo `Id`, `TarefaId`, `UsuarioId`, `Acao`, `DataUtc` e `Descricao`, sem serializar entidades EF.
- `page` começa em 1 e aceita até 1.000.000; `pageSize` padrão 20, máximo 100. Valores inválidos retornam 400. O repositório usa `AsNoTracking`, executa contagem separada e ordena/pagina no SQL Server por `DataUtc`, depois `Id`, sem carregar o histórico inteiro.
- Nenhuma rota HTTP de escrita/exclusão foi adicionada ao histórico; POST/DELETE no recurso recebem 405. A FK da migration existente relaciona o evento à tarefa; os testes confirmam autor, UTC e exatamente um `CriarTarefa` no fluxo de criação.
- Sem nova migration/schema. O fixture verifica nome `TaskFlowIntegrationTests_<GUID>` e cria/remove exclusivamente banco temporário. O baseline inicial encontrou um teste preexistente que abria conexão read-only com `TaskFlowDev` apenas via `CanConnectAsync`; removi essa conexão e mantive a verificação do catálogo. A suíte final não conecta nem grava em `TaskFlowDev`; nenhum bootstrap foi executado.

### Verificação da história completa e testes finais

Fluxo HTTP/SQL Server: administrador autenticado cria um funcionário pela API; cria tarefa atribuída; lê detalhe e histórico; responsável lê detalhe e histórico; funcionário sem vínculo é bloqueado; anônimo recebe 401. Verificações adicionais comprovam histórico vazio para tarefa existente, paginação total por página, desempate determinístico por Id quando datas UTC coincidem, e validações de page/pageSize.

| Verificação | Aprovados | Reprovados | Ignorados |
| --- | ---: | ---: | ---: |
| Baseline anterior à alteração (`dotnet test TaskFlow.sln`) | 71 | 0 | 0 |
| `dotnet restore TaskFlow.sln` | Sucesso | — | — |
| `dotnet build TaskFlow.sln` | 0 avisos | 0 erros | — |
| Domínio (`Category=Domain`) | 25 | 0 | 0 |
| HTTP (`Category=Http`) | 35 | 0 | 0 |
| SQL Server (`Category=SqlServer`) | 41 | 0 | 0 |
| Segurança (`Category=Security`) | 2 | 0 | 0 |
| Suíte final (`dotnet test TaskFlow.sln`) | **74** | **0** | **0** |

As categorias HTTP e SQL Server se sobrepõem nos testes integrados; não devem ser somadas. As 17 verificações de `TaskEndpointTests` passaram, incluindo as três novas de histórico/fluxo completo. O teste de rollback existente continuou passando e confirma zero tarefa/evento após falha do repositório de histórico dentro da transação.

### Revisão e retrospectiva da Sprint 1

A revisão antecipada de 10/11 concluiu US01–US05 com critérios cobertos pelos testes e evidências listados em [backlog.md](backlog.md). A demonstração manual em `TaskFlowDev` não é considerada executada; ela permanece pendente de segredo e credenciais escolhidos pelo mantenedor.

- **O que funcionou:** o SQL Server real em fixtures efêmeros comprovou integridade e permissões sem modificar o banco de desenvolvimento; os testes end-to-end verificam a cadeia API → repositórios → SQL.
- **Dificuldades observadas:** mapeamento de RowVersion, atomicidade entre chamadas individuais de `SaveChangesAsync` e validações HTTP integradas exigiram correções nos dias anteriores. Os contratos reaproveitaram a unidade de trabalho/repositórios, sem acrescentar camadas.
- **Decisões:** migrations continuam sendo a única estratégia de schema; a aplicação é a única origem do evento, sem triggers de auditoria; histórico é paginado no servidor e somente leitura.
- **Ajuste da Sprint 2:** priorizar listagem geral/filtros e atualização com RowVersion e histórico. A consulta de detalhe e histórico paginado foram antecipadas. Rever escopo de web e programação SQL diante da capacidade e dependências, sem declarar implementação antes de testá-la.
