# Alunos Tech TaskFlow — PIM VIII

Sistema acadêmico da UNIP para organizar tarefas internas da empresa fictícia Alunos Tech. Integra Cloud Computing e DevOps, Gerenciamento de Projetos Ágil, Programação Aplicada em .NET e Programação de Banco de Dados.

## Estado da implementação

**Dia 4 — cadastro e atribuição de tarefas:** além da autenticação JWT e gestão administrativa de usuários, a API agora permite que somente administradores criem tarefas, opcionalmente atribuindo-as a funcionários. O criador vem exclusivamente do token validado; status inicia como Pendente; tarefa e evento `CriarTarefa` são persistidos na mesma transação. `GET /api/tasks/{id}` permite acesso a administradores e a funcionários vinculados como criador ou responsável. O bootstrap e a demonstração manual em `TaskFlowDev` permanecem dependentes de credenciais escolhidas pelo mantenedor. Nenhuma migration ou alteração de schema foi necessária.

Validação final: **71 aprovados, 0 reprovados e 0 ignorados**; restore concluído e build sem avisos/erros. Os resultados detalhados, incluindo testes HTTP e SQL Server reais, estão em [ambiente](docs/ambiente.md) e [diário](docs/diario-desenvolvimento.md).

`Category=Http` (32) e `Category=SqlServer` (38) se sobrepõem nos testes HTTP integrados que usam SQL isolado; os contadores filtrados não devem ser somados. `Category=Domain` teve 25 e `Category=Security` teve 2.

## Tecnologias e requisitos

- SDK **.NET 10 LTS**, runtime ASP.NET Core 10 e Git. `global.json` seleciona 10.0.403, que está instalado no ambiente.
- ASP.NET Core Minimal API, Entity Framework Core 10, OpenAPI/Swagger e xUnit.
- Acesso ao NuGet para o primeiro restore, ou os pacotes necessários em cache.
- SQL Server local em desenvolvimento real, usando a base `TaskFlowDev` com migrations EF Core e conexão por autenticação integrada.
- Flutter e Docker não são requisitos para executar os projetos atuais.

A solução foi migrada para **.NET 10 LTS** e validada com SDK 10.0.403 instalado no ambiente. A estratégia de persistência usa EF Core 10 e migrations oficiais em SQL Server local, sem segredos versionados no repositório. [Política de suporte da Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Organização

```text
TaskFlow.sln
src/
  TaskFlow.Api/              # HTTP, DI e Swagger
  TaskFlow.Application/      # Contratos e informações da aplicação
  TaskFlow.Domain/           # Entidades, enums e invariantes
  TaskFlow.Infrastructure/   # EF Core, migrations e repositórios SQL Server
  TaskFlow.Web/              # Reservado
  TaskFlow.Desktop/          # Reservado
mobile/                     # Reservado para Flutter
database/scripts/001_schema.sql
infra/docker/               # Reservado
infra/k8s/                  # Reservado
tests/TaskFlow.Tests/        # Domínio, HTTP/JWT e integração SQL Server
docs/                       # Requisitos, decisões, planejamento e evidências
```

Os diretórios reservados contêm somente `.gitkeep` e não integram a solução como projetos fictícios. O PDF local preexistente foi preservado e excluído do versionamento desta etapa.

## Executar localmente

Na raiz do repositório, em PowerShell ou outro terminal com o SDK disponível:

```powershell
dotnet --version
dotnet restore TaskFlow.sln
dotnet build TaskFlow.sln --no-restore
dotnet test TaskFlow.sln
dotnet run --project src/TaskFlow.Api --no-build --no-restore --launch-profile http
```

A API usa `http://localhost:5046` nesse perfil e o ambiente `Development`. Acesse [Swagger](http://localhost:5046/swagger) ou consulte em outro terminal:

```powershell
Invoke-RestMethod http://localhost:5046/health
Invoke-RestMethod http://localhost:5046/api/info
```

`src/TaskFlow.Api/TaskFlow.Api.http` contém as mesmas requisições. Encerre a API com `Ctrl+C`.

| Rota | Resposta implementada |
| --- | --- |
| `GET /health` | HTTP 200, `status: "Healthy"` e `timestampUtc` atual em UTC |
| `GET /api/info` | HTTP 200, `name: "TaskFlow"`, `version: "0.1.0"` e descrição |
| `POST /api/auth/login` | Valida e-mail/senha e emite token de acesso assinado com duração de 60 minutos |
| `GET /api/auth/me` | Requer JWT e devolve o perfil atual do usuário autenticado |
| `POST /api/users` | Somente Administrador; cria funcionário ou administrador |
| `GET /api/users` | Somente Administrador; listagem paginada, `pageSize` máximo 100 |
| `GET /api/users/{id}` | Somente Administrador; consulta pública ou 404 |
| `PUT /api/users/{id}` | Somente Administrador; atualiza nome, e-mail e perfil |
| `POST /api/tasks` | Somente Administrador; cria tarefa e histórico atomicamente; responsável funcionário opcional |
| `GET /api/tasks/{id}` | JWT obrigatório; Administrador consulta qualquer tarefa, funcionário somente se criador ou responsável |
| `GET /swagger/v1/swagger.json` | Especificação OpenAPI, apenas em desenvolvimento |
| `GET /swagger/index.html` | Interface Swagger, apenas em desenvolvimento |

Rotas administrativas retornam 401 sem token e 403 para funcionário. O perfil é revalidado no SQL Server em toda requisição autenticada, de modo que mudança de perfil invalida privilégios antigos. A resposta de tarefa apresenta RowVersion em Base64. A lista paginada, atualizações/reatribuições e consulta de histórico por rota ainda não foram implementadas. `/health` é uma verificação de vida do processo HTTP; não verifica dependências externas. Nullable está habilitado e warnings são tratados como erros por `Directory.Build.props`.

## Banco e configuração

**EF Core migrations são o único mecanismo oficial de criação/evolução do schema.** `database/scripts/001_schema.sql` permanece somente como referência e não foi executado. `InitialCreate`, `HardenPersistenceMappings` e `DatabaseDefaults` estão aplicadas em `TaskFlowDev`; não se usa `EnsureCreated()` nem migração automática no startup. O banco existente foi preservado.

Em Development, a API lê `ConnectionStrings:TaskFlow` de `src/TaskFlow.Api/appsettings.Development.json` e usa autenticação integrada para `TaskFlowDev`; não há senha no repositório. Ambientes sem essa configuração falham explicitamente. A suíte SQL cria um banco temporário `TaskFlowIntegrationTests_<GUID>` por execução, aplica migrations e remove apenas esse banco gerado. Nunca usa `TaskFlowDev`. Para outro SQL Server, defina `TASKFLOW_TEST_SQLSERVER_CONNECTION_STRING` fora do repositório; a conta precisa de permissão para criar e remover bancos de teste. Credenciais de usuários e hashes não são fornecidos.

### Segredo JWT e primeiro administrador (PowerShell)

A chave JWT deve ser Base64 de ao menos 32 bytes aleatórios e fica somente no User Secrets ou variável externa. Configure-a localmente:

```powershell
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Authentication:Jwt:SigningKey" $jwtKey --project src/TaskFlow.Api
Remove-Variable jwtKey
```

Em outros ambientes, configure `Authentication__Jwt__SigningKey`, `Authentication__Jwt__Issuer` e `Authentication__Jwt__Audience` por variáveis seguras do ambiente/orquestrador. A ausência da chave nunca gera fallback.

Em seguida, execute **explicitamente** o bootstrap; ele solicita nome/e-mail no terminal e lê a senha sem exibi-la. Só aceita o banco `TaskFlowDev`, não aplica migrations e não substitui administrador existente:

```powershell
dotnet run --project src/TaskFlow.Api --launch-profile http -- --bootstrap-admin
```

Para testar os endpoints, execute a API em Development, faça login em `POST /api/auth/login`, copie o access token para **Authorize** no Swagger e use `GET /api/auth/me` ou as rotas administrativas. Nunca registre ou compartilhe o token.

Senhas são armazenadas com `PasswordHasher<Usuario>` do ASP.NET Core Identity (salt e derivação própria para senhas); mínimo de 12 caracteres, máximo de 128, com rehash automático quando necessário. E-mail é normalizado e a unicidade é garantida pela constraint SQL Server; conflitos retornam 409. Nenhum endpoint expõe `PasswordHash`.

Para executar testes por categoria:

```powershell
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=Domain"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=Http"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=SqlServer"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=Security"
```

## Documentação e próximos passos

- [Escopo, RF01–RF08, RNF01–RNF08 e permissões](docs/escopo.md)
- [Arquitetura e comunicação entre clientes, API e SQL Server](docs/arquitetura.md)
- [Modelo de dados e objetos SQL planejados](docs/modelo-dados.md)
- [Backlog com três sprints](docs/backlog.md)
- [Cronograma de 09 a 22/11/2026](docs/cronograma.md)
- [Diagnóstico e verificações](docs/ambiente.md)
- [Diário de desenvolvimento](docs/diario-desenvolvimento.md)

Pendências próximas: executar manualmente o bootstrap com uma senha definida pelo mantenedor e demonstrar o fluxo no Swagger em `TaskFlowDev`; implementar consulta paginada e alterações de tarefas no Dia 5/6. Interfaces, objetos SQL, Docker, Kubernetes e CI/CD permanecem planejados. O plano de duas semanas não representa a data oficial de entrega da instituição.
