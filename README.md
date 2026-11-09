# Alunos Tech TaskFlow — PIM VIII

Sistema acadêmico da UNIP para organizar tarefas internas da empresa fictícia Alunos Tech. Integra Cloud Computing e DevOps, Gerenciamento de Projetos Ágil, Programação Aplicada em .NET e Programação de Banco de Dados.

## Estado da implementação

**Dia 1 — 09/11/2026:** solução .NET 8 com domínio validado, metadados da aplicação por DI, API mínima, testes xUnit, SQL Server de referência e planejamento. Não há persistência conectada, autenticação, CRUD ou clientes implementados. Web, desktop, Flutter, Docker, Kubernetes e CI/CD estão planejados para as próximas etapas.

A validação desta entrega terminou com **30 testes aprovados**, build sem avisos/erros e respostas HTTP reais dos endpoints. Os comandos e limites estão em [ambiente](docs/ambiente.md) e no [diário](docs/diario-desenvolvimento.md).

## Tecnologias e requisitos

- SDK **.NET 8.0.4xx**, runtime ASP.NET Core 8 e Git. `global.json` seleciona o último patch instalado da faixa 8.0.400; este ambiente usou 8.0.427.
- ASP.NET Core Minimal API, OpenAPI/Swagger e xUnit com testes de integração por `WebApplicationFactory`.
- Acesso ao NuGet para o primeiro restore, ou os pacotes necessários em cache.
- SQL Server será necessário para a persistência futura. A API do Dia 1 inicia sem banco e sem connection string.
- Flutter e Docker não são requisitos para executar os projetos atuais.

O .NET 8 foi mantido conforme a base solicitada. O suporte oficial termina em **10/11/2026**; a reavaliação do .NET 10 já instalado está prevista para o Dia 2, antes da implantação. [Política de suporte da Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Organização

```text
TaskFlow.sln
src/
  TaskFlow.Api/              # HTTP, DI e Swagger
  TaskFlow.Application/      # Informações da aplicação; casos de uso futuros
  TaskFlow.Domain/           # Entidades, enums e invariantes
  TaskFlow.Infrastructure/   # Projeto para persistência futura
  TaskFlow.Web/              # Reservado
  TaskFlow.Desktop/          # Reservado
mobile/                     # Reservado para Flutter
database/scripts/001_schema.sql
infra/docker/               # Reservado
infra/k8s/                  # Reservado
tests/TaskFlow.Tests/        # 25 casos de domínio e 5 de API
docs/                       # Requisitos, decisões, planejamento e evidências
```

Os diretórios reservados contêm somente `.gitkeep` e não integram a solução como projetos fictícios. O PDF local preexistente foi preservado e excluído do versionamento desta etapa.

## Executar localmente

Na raiz do repositório, em PowerShell ou outro terminal com o SDK disponível:

```powershell
dotnet --version
dotnet restore TaskFlow.sln
dotnet build TaskFlow.sln --no-restore
dotnet test TaskFlow.sln --no-build --no-restore
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

`/health` é uma verificação de vida do processo HTTP; não verifica banco nem dependências externas. Os testes de integração usam o host real da aplicação em memória; a execução por HTTP/TCP também foi verificada separadamente. Nullable está habilitado e warnings são tratados como erros por `Directory.Build.props`.

## Banco e configuração

O [schema SQL](database/scripts/001_schema.sql) é **referência técnica**, não foi executado e não cria banco. Contém as três tabelas, constraints, índice e `rowversion`, com transação e proteção contra aplicação sobre objetos existentes ou bancos de sistema. A preferência é adotar **EF Core migrations como única estratégia oficial no Dia 2**. Não executar SQL e migration inicial equivalentes no mesmo banco. Detalhes no [modelo de dados](docs/modelo-dados.md).

Não há segredos nem variáveis adicionais obrigatórias nesta etapa; por isso não há `.env.example`. A configuração atual contém apenas logging e hosts. Senhas devem chegar ao campo `PasswordHash` já transformadas pelo componente apropriado, a ser implementado com a autenticação. Nenhuma senha ou usuário inicial é fornecido. Para conexões futuras, usar configuração externa/segredos locais e nunca versionar credenciais.

## Documentação e próximos passos

- [Escopo, RF01–RF08, RNF01–RNF08 e permissões](docs/escopo.md)
- [Arquitetura e comunicação entre clientes, API e SQL Server](docs/arquitetura.md)
- [Modelo de dados e objetos SQL planejados](docs/modelo-dados.md)
- [Backlog com três sprints](docs/backlog.md)
- [Cronograma de 09 a 22/11/2026](docs/cronograma.md)
- [Diagnóstico e verificações](docs/ambiente.md)
- [Diário de desenvolvimento](docs/diario-desenvolvimento.md)

No Dia 2: decidir a versão suportada do .NET, confirmar a estratégia de schema, configurar EF Core/SQL Server sem segredos versionados e validar a primeira migration, unicidade, FKs e concorrência em banco de desenvolvimento real. O plano de duas semanas não representa a data oficial de entrega da instituição.
