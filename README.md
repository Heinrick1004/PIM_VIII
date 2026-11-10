# Alunos Tech TaskFlow — PIM VIII

Sistema acadêmico da UNIP para organizar tarefas internas da empresa fictícia Alunos Tech. Integra Cloud Computing e DevOps, Gerenciamento de Projetos Ágil, Programação Aplicada em .NET e Programação de Banco de Dados.

## Estado da implementação

**Dia 2 — 10/11/2026:** solução migrada para .NET 10 com EF Core 10, SQL Server, migrations, repositórios e unidade transacional. O banco existente `TaskFlowDev` foi atualizado sem apagar/recriar o banco; não há ainda autenticação nem CRUD HTTP. Web, desktop, Flutter, Docker, Kubernetes e CI/CD continuam fora do escopo atual.

A validação real do Dia 2 terminou com **42 testes aprovados, 0 reprovados e 0 ignorados**: 25 de domínio, 6 de API/HTTP e 11 de integração em SQL Server real. Build: 0 avisos e 0 erros. Os resultados verificáveis estão em [ambiente](docs/ambiente.md), [modelo de dados](docs/modelo-dados.md) e [diário](docs/diario-desenvolvimento.md).

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
  TaskFlow.Application/      # Informações da aplicação; casos de uso futuros
  TaskFlow.Domain/           # Entidades, enums e invariantes
  TaskFlow.Infrastructure/   # EF Core, migrations e repositórios SQL Server
  TaskFlow.Web/              # Reservado
  TaskFlow.Desktop/          # Reservado
mobile/                     # Reservado para Flutter
database/scripts/001_schema.sql
infra/docker/               # Reservado
infra/k8s/                  # Reservado
tests/TaskFlow.Tests/        # 25 domínio, 6 API/HTTP e 10 SQL Server
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
| `GET /swagger/v1/swagger.json` | Especificação OpenAPI, apenas em desenvolvimento |
| `GET /swagger/index.html` | Interface Swagger, apenas em desenvolvimento |

`/health` é uma verificação de vida do processo HTTP; não verifica banco nem dependências externas. Nullable está habilitado e warnings são tratados como erros por `Directory.Build.props`.

## Banco e configuração

**EF Core migrations são o único mecanismo oficial de criação/evolução do schema.** `database/scripts/001_schema.sql` permanece somente como referência e não foi executado. `InitialCreate`, `HardenPersistenceMappings` e `DatabaseDefaults` estão aplicadas em `TaskFlowDev`; não se usa `EnsureCreated()` nem migração automática no startup. O banco existente foi preservado.

Em Development, a API lê `ConnectionStrings:TaskFlow` de `src/TaskFlow.Api/appsettings.Development.json` e usa autenticação integrada para `TaskFlowDev`; não há senha no repositório. Ambientes sem essa configuração falham explicitamente. A suíte SQL cria um banco temporário `TaskFlowIntegrationTests_<GUID>` por execução, aplica migrations e remove apenas esse banco gerado. Nunca usa `TaskFlowDev`. Para outro SQL Server, defina `TASKFLOW_TEST_SQLSERVER_CONNECTION_STRING` fora do repositório; a conta precisa de permissão para criar e remover bancos de teste. Credenciais de usuários e hashes não são fornecidos.

Para executar testes por categoria:

```powershell
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=Domain"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=Http"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=SqlServer"
```

## Documentação e próximos passos

- [Escopo, RF01–RF08, RNF01–RNF08 e permissões](docs/escopo.md)
- [Arquitetura e comunicação entre clientes, API e SQL Server](docs/arquitetura.md)
- [Modelo de dados e objetos SQL planejados](docs/modelo-dados.md)
- [Backlog com três sprints](docs/backlog.md)
- [Cronograma de 09 a 22/11/2026](docs/cronograma.md)
- [Diagnóstico e verificações](docs/ambiente.md)
- [Diário de desenvolvimento](docs/diario-desenvolvimento.md)

Próximas etapas: casos de uso e endpoints de negócio, autenticação/autorização e clientes planejados. O plano de duas semanas não representa a data oficial de entrega da instituição.
