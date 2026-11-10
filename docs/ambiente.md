# Diagnóstico do ambiente e validação — Dias 1 a 5

## Situação verificada no Dia 5 — 10/11/2026

| Item | Resultado |
| --- | --- |
| Branch/base | `main`, commit-base `3a8167d8c8e21fde771a0ba5fc0e8512a8b6838e`, confirmado em `origin/main` após `git fetch`. |
| Restore | `dotnet restore TaskFlow.sln`: sucesso. |
| Build | `dotnet build TaskFlow.sln`: sucesso, **0 avisos e 0 erros**. |
| Suíte completa | Baseline antes das alterações: **71 aprovados, 0 reprovados, 0 ignorados**. Resultado final Dia 5: **74 aprovados, 0 reprovados, 0 ignorados**. |
| Domínio | `Category=Domain`: **25 aprovados, 0 reprovados, 0 ignorados**. |
| HTTP | `Category=Http`: **35 aprovados, 0 reprovados, 0 ignorados**. |
| SQL Server | `Category=SqlServer`: **41 aprovados, 0 reprovados, 0 ignorados**, em SQL Server real e bancos temporários isolados. |
| Segurança | `Category=Security`: **2 aprovados, 0 reprovados, 0 ignorados**. |
| Categorias | HTTP e SQL Server se sobrepõem nos testes HTTP que usam banco SQL isolado; os resultados filtrados não devem ser somados. |
| Cobertura nova | Testes cobrem criação e histórico paginado/cronológico, administrador/criador/responsável, 401/403/404, histórico vazio, paginação/empates por Id, UTC, métodos de escrita não permitidos e fluxo completo por HTTP/SQL. |
| Banco de teste | Fixture SQL Server real usa `TaskFlowIntegrationTests_<GUID>`, migra e remove apenas cada banco temporário gerado. |
| `TaskFlowDev` | Não foi escrito, limpo, recriado nem migrado. No baseline, o teste anterior chamou `CanConnectAsync` (tentativa de conexão somente, sem query de negócio ou escrita); esse acesso foi removido. A suíte final verifica apenas o catálogo configurado, sem abrir conexão, e testes SQL apontam para catálogos temporários. Nenhum bootstrap foi executado. |
| Schema | Nenhuma migration foi criada ou modificada; o modelo existente já contém as colunas, FKs e RowVersion necessárias. |
| Segredos/artefatos | Nenhuma credencial real ou token foi adicionado; a senha presente nos testes é fixa e exclusiva do fixture SQL isolado. Nenhum artefato temporário entrou no conjunto de alterações. |

O detalhe retorna 201 com `Location` e RowVersion Base64; o mesmo token de autorização protege detalhe para administrador ou criador/responsável. A integração de criação e histórico usa `IUnitOfWork`; o teste de falha injeta erro após o repositório salvar o histórico na transação e confirma por novo contexto SQL que nenhuma das duas linhas permanece. Testes também confirmam um único evento `CriarTarefa`.

O bootstrap e a demonstração manual em `TaskFlowDev` permanecem pendentes até o mantenedor definir credenciais. A validação final foi automatizada com usuários e senha exclusivos dos testes no banco temporário; nenhum dos testes finais abre conexão com `TaskFlowDev`.

O teste ponta a ponta cria funcionário pela API autenticada, cria tarefa atribuída, consulta detalhe e histórico como administrador, acessa com o responsável, e confirma bloqueio a funcionário sem vínculo e a cliente anônimo. O endpoint do histórico retorna coleção vazia para tarefa existente sem eventos; valida `page` (1 a 1.000.000) e `pageSize` (1 a 100, default 20). Nenhuma query de persistência materializa todo o histórico antes de paginar.

### Fechamento da Sprint 1

US01–US05 foram revisadas e seus critérios de aceite documentados como atendidos; a tabela de evidências por história e a retrospectiva estão em [backlog.md](backlog.md). A revisão ocorreu em 10/11, antes do fim planejado da Sprint 1, conforme solicitado. A demonstração manual em `TaskFlowDev` é uma pendência operacional separada, pois exige credencial definida pelo mantenedor.

## Situação verificada no Dia 3 — 10/11/2026

| Item | Resultado |
| --- | --- |
| Branch/base | `main`, commit-base `bdefa97fb213610670791ff0862f514402961efe`, sincronizado com `origin/main` antes das alterações. |
| Restore | `dotnet restore TaskFlow.sln`: sucesso. |
| Build | `dotnet build TaskFlow.sln --no-restore`: sucesso, **0 avisos e 0 erros**. |
| Suíte completa | `dotnet test TaskFlow.sln`: **52 aprovados, 0 reprovados, 0 ignorados**. |
| Domínio | `Category=Domain`: **25 aprovados, 0 reprovados, 0 ignorados**. |
| HTTP/middleware | `Category=Http`: **13 aprovados, 0 reprovados, 0 ignorados**. Os sete novos fluxos de autenticação/usuários atravessam o middleware ASP.NET Core e SQL Server isolado. |
| SQL Server | `Category=SqlServer`: **19 aprovados, 0 reprovados, 0 ignorados**. Inclui os 11 testes de persistência do Dia 2, os sete testes HTTP integrados e um teste de bootstrap. |
| Segurança de configuração | `Category=Security`: **2 aprovados, 0 reprovados, 0 ignorados**; chave ausente ou menor que 256 bits é rejeitada. |
| Categorias | HTTP e SQL Server se sobrepõem nos sete testes integrados de endpoints; por isso, os contadores filtrados não devem ser somados. |
| Banco de testes | Cada fixture utilizou `TaskFlowIntegrationTests_<GUID>`, criado/aplicado migrations/removido pelo fixture. Nenhum banco temporário permaneceu após os testes. |
| `TaskFlowDev` | Consulta somente leitura confirmou o mesmo banco, migrations `InitialCreate`, `HardenPersistenceMappings` e `DatabaseDefaults`, e 0 linhas em `Usuarios`, `Tarefas` e `HistoricoTarefas`. Não foram aplicadas migrations nem executadas gravações no Dia 3. |
| Configuração JWT | `Issuer`/`Audience` são parâmetros de configuração; `SigningKey` deve ser Base64 externo, mínimo 32 bytes. Nenhuma chave JWT real está versionada. |
| Bootstrap manual | Rotina testada no banco isolado. Não foi executada em `TaskFlowDev`, pois nenhuma senha inicial foi fornecida/escolhida pelo mantenedor. |

Testes HTTP adicionais cobrem login com senha correta/incorreta e e-mail inexistente, hash legado com rehash, claims mínimas, expiração, assinatura adulterada, issuer/audience incorretos, `/me`, 401/403, autorização administrativa, perfil revalidado, cadastro/listagem/paginação/consulta/atualização, e-mail duplicado e proteção do último administrador. O bootstrap SQL provou criação com hash e execução repetida sem duplicar o primeiro administrador.

Não houve alteração de schema, nova migration, `EnsureCreated`, nem execução de script SQL. A API não executa migrations no startup.

## Situação verificada no Dia 2 — 10/11/2026

O estado abaixo foi medido novamente nesta execução; os detalhes do Dia 1 foram preservados como histórico na seção seguinte.

| Item | Resultado |
| --- | --- |
| SDK selecionado | `dotnet --version`: **10.0.403**, selecionado por `global.json`; SDKs 8.0.427, 10.0.401 e 10.0.403 instalados. Runtimes ASP.NET Core/.NET 10 disponíveis. |
| SQL Server | Serviço `MSSQLSERVER` ativo; SQL Server 2025, versão 17.0.1145.4. Conexão integrada real via `sqlcmd -S localhost -E -C` aprovada. |
| `TaskFlowDev` antes de migrations corretivas | Banco existente confirmado; histórico continha `InitialCreate` e três tabelas sem dados (0 Usuarios, 0 Tarefas, 0 HistoricoTarefas). |
| `TaskFlowDev` após migrations | Mesmo banco, com `InitialCreate`, `HardenPersistenceMappings` e `DatabaseDefaults`. `Versao` é `rowversion NOT NULL`; e-mail usa `Latin1_General_100_CI_AS`. Nenhuma linha foi apagada ou recriada. |
| Banco de teste SQL | `TaskFlowIntegrationTests_<GUID>`, criado por cada execução da suíte, migrations aplicadas e banco removido ao final. Suíte completa final: `TaskFlowIntegrationTests_fd03a55e59f54bf096103f6a555dfd0e`; execução filtrada: `TaskFlowIntegrationTests_f05850514fb440af9e70301c85493240`. Nunca usa `TaskFlowDev`. |
| Conexão da API Development | `ConnectionStrings:TaskFlow` em `appsettings.Development.json`; autenticação integrada e catálogo `TaskFlowDev`. Teste de API resolve o contexto, confirma o catálogo e abre conexão real (`CanConnectAsync`). A resolução da persistência sem configuração falha explicitamente, sem fallback oculto. |
| Credenciais | Varredura dos arquivos de código/configuração não encontrou senhas ou secrets. A connection string versionada contém apenas autenticação integrada para ambiente local. A connection string opcional dos testes vem de `TASKFLOW_TEST_SQLSERVER_CONNECTION_STRING` fora do repositório. |
| Restore | `dotnet restore TaskFlow.sln`: sucesso. |
| Build | `dotnet build TaskFlow.sln --no-restore --nologo`: sucesso, **0 avisos e 0 erros**. |
| Testes de domínio | `Category=Domain`: **25 aprovados, 0 reprovados, 0 ignorados**. |
| Testes de API/HTTP | `Category=Http`: **6 aprovados, 0 reprovados, 0 ignorados**. Inclui endpoints HTTP e DI apontando para `TaskFlowDev`. |
| Testes SQL Server | `Category=SqlServer`: **11 aprovados, 0 reprovados, 0 ignorados** em SQL Server real. |
| Total | `dotnet test TaskFlow.sln`: **42 aprovados, 0 reprovados, 0 ignorados**. |

Os testes SQL verificam inserção/consulta de Usuario e Tarefa, email duplicado com variação de caixa, defaults de banco, enums, UTC, FKs, histórico, filtros e paginação estável, RowVersion com dois contextos, commit/rollback e atualização+histórico atomicamente. `DbUpdateConcurrencyException` foi observado na escrita obsoleta; a atualização mais recente permaneceu no banco. Uma falha de FK ao gravar histórico reverteu a atualização de tarefa na mesma transação. Repetir uma alteração sem mudança retornou `null` e permaneceu apenas um evento de histórico.

### Problemas corrigidos durante a validação

- Mapeamento de `_rowVersion` usava propriedade sombra e criava `Versao` anulável. O mapeamento agora associa o backing field à propriedade `RowVersion`, exige 8 bytes e configura o token de concorrência. A correção é uma migration posterior, não uma recriação da inicial.
- SQL Server não permite alterar diretamente o tipo rowversion existente. A migration corretiva remove e recria somente a coluna gerada `Versao`, mantendo linhas e dados de negócio. Na aplicação em `TaskFlowDev`, a tabela estava vazia, confirmado antes da alteração.
- A alteração de collation de `Email` dependia de check constraint e índice único. A migration agora remove e recria esses objetos na ordem correta.
- `datetime2` não armazena `DateTimeKind`; conversores EF garantem UTC na materialização.
- A paginação por criação agora desempata por Id; limites inválidos são rejeitados.
- Repositórios salvavam cada chamada separadamente; unidade de trabalho explícita preserva atomicidade entre chamadas dentro da mesma transação scoped.
- Configuração sem connection string não aponta silenciosamente para `TaskFlowDev`.

O schema é gerenciado exclusivamente pelas migrations EF Core. `database/scripts/001_schema.sql` não foi executado. A API não aplica migrations automaticamente na inicialização.

## Histórico documentado do Dia 1 (09/11/2026)

Referência e execução: **09/11/2026**, fuso do usuário America/Sao_Paulo. Nenhum SDK, Docker, Flutter ou SQL Server foi instalado por esta atividade.

## Estado inicial e decisão de versão

A pasta já continha `TaskFlow.sln`, cinco projetos .NET 8, entidades, testes iniciais, configurações da API e SQL de referência. `docs`, `mobile` e os diretórios de infraestrutura existiam, com documentação ainda ausente. `Manual.pdf` preexistente foi preservado byte a byte e ignorado no Git. Não foram encontrados arquivos `AGENTS.md` no projeto ou nos diretórios ascendentes consultados.

Os comandos iniciais `git status --short --branch`, `git branch --show-current` e `git remote -v` retornaram `fatal: not a git repository`. A consulta somente de leitura ao GitHub confirmou o repositório [Heinrick1004/PIM_VIII](https://github.com/Heinrick1004/PIM_VIII) acessível e sem branches nesse momento. Foi inicializado Git local em `main` e configurado `origin` com a URL fornecida. Não houve push ou criação de PR.

Os cinco projetos permanecem em `net8.0` conforme preferência do projeto e SDK disponível. `global.json` usa versão base `8.0.400`, `rollForward: latestPatch` e não permite previews; a seleção verificada é **8.0.427**. O .NET 8 encerra suporte em **10/11/2026**, durante o plano; avaliar .NET 10 antes de implantação, sem instalar outro SDK nesta etapa. [Política oficial da Microsoft, consultada em 09/11/2026](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Ferramentas

| Item | Situação | Evidência e limites |
| --- | --- | --- |
| Windows / PowerShell | Presente | Windows x64; shell PowerShell. `dotnet --info` identificou Windows 10.0.26300 e RID win-x64. |
| Git | Presente | `git --version`: 2.56.0.windows.2. Identidade local já configurada; seus valores pessoais não foram copiados para a documentação. |
| SDK .NET 8 | Presente | `dotnet --list-sdks`: 8.0.427. `dotnet --version` após `global.json`: 8.0.427. |
| SDK .NET 10 | Presente | 10.0.401 e 10.0.403. Disponível para reavaliação no Dia 2; projetos não foram migrados. |
| Runtimes .NET 8 | Presente | Microsoft.NETCore.App, Microsoft.AspNetCore.App e WindowsDesktop.App 8.0.33. |
| SQL Server | Presente e conexão de leitura verificada | Serviço `MSSQLSERVER` em execução. Consulta a `SERVERPROPERTY` via autenticação integrada: versão 17.0.1145.4, Enterprise Evaluation Edition (64-bit). Nenhum objeto foi criado/alterado. |
| sqlcmd | Presente | Executável no PATH; `sqlcmd -?` mostrou 17.0.1000.7. |
| Docker CLI / Desktop | Ausente nas verificações realizadas | `Get-Command docker` não encontrou comando; caminhos padrão do Docker Desktop e CLI também ausentes. Instalação em outro diretório e daemon remoto não verificados. |
| Flutter / Dart | Presente | `flutter --version`: Flutter 3.47.9 stable, Dart 3.13.7 e DevTools 2.60.0. |
| Emuladores e toolchains mobile | Não verificado | Não foi executado `flutter doctor`, criado app ou iniciado emulador. |
| Kubernetes / cluster e nuvem | Não verificado | Não necessários para o Dia 1; nenhum recurso provisionado. |
| Schema e integração SQL TaskFlow | Não executado | Apenas revisão estática do SQL; não há banco TaskFlow criado por esta atividade, migrations ou persistência conectada. |

A edição Evaluation foi identificada apenas para diagnóstico. Licenciamento e disponibilidade de infraestrutura de implantação permanecem por definir com a evolução do projeto.

## Comandos e resultados reais

| Comando/ação | Resultado |
| --- | --- |
| `dotnet --info`, `dotnet --list-sdks`, `git --version`, `Get-Command` e consulta aos serviços SQL | Diagnóstico acima, sem instalações. |
| `flutter --version` | Sucesso, versão 3.47.9. |
| `sqlcmd -S localhost -E -C -l 5 -b -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion, CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition;"` | Sucesso; somente leitura. `-C` foi usado para confiar no certificado da instância local de desenvolvimento; não define a política TLS da futura implantação. |
| `git init --initial-branch=main` e `git remote add origin https://github.com/Heinrick1004/PIM_VIII.git` | Sucesso; repositório local inicializado e origin configurado. |
| `dotnet sln TaskFlow.sln list` | Cinco projetos válidos listados. |
| `dotnet restore TaskFlow.sln` | Sucesso nos cinco projetos. |
| `dotnet build TaskFlow.sln --no-restore` | Sucesso, **0 avisos e 0 erros**. |
| `dotnet test TaskFlow.sln --no-build --no-restore --logger "trx;LogFileName=day1.trx" --results-directory artifacts/test-results` | **30 aprovados, 0 reprovados, 0 ignorados**. |
| API compilada em processo local + `Invoke-WebRequest` | HTTP 200 nos quatro caminhos verificados; detalhes abaixo. Processo de verificação encerrado ao final. |

Após a inicialização do Git, o executor em sandbox passou a falhar antes de criar processos (`setup refresh had errors`). Os comandos necessários foram executados fora do sandbox mediante revisão automática, mantendo o escopo autorizado. A propriedade do `.git` criado pelo usuário do sandbox também exigiu `git -c safe.directory='<caminho absoluto deste projeto>' ...` nos comandos Git seguintes. A exceção foi limitada a cada comando; não se alterou a configuração global de diretórios confiáveis.

## Evidências HTTP e testes

O binário foi iniciado a partir de `src/TaskFlow.Api`, com ambiente Development e URL `http://127.0.0.1:15046`, usando `dotnet bin/Debug/net8.0/TaskFlow.Api.dll --urls http://127.0.0.1:15046 --environment Development`. A porta alternativa foi usada somente para a verificação; o perfil de desenvolvimento continua em 5046.

| Requisição | Resultado observado |
| --- | --- |
| `GET /health` | 200; `status = Healthy`; `timestampUtc = 2026-11-09T20:23:22.1466759Z`, também comparado ao relógio real com tolerância de 30 segundos. |
| `GET /api/info` | 200; `name = TaskFlow`, `version = 0.1.0` e descrição da API inicial. |
| `GET /swagger/v1/swagger.json` | 200; especificação contém `/health` e `/api/info`. |
| `GET /swagger/index.html` | 200; Swagger UI disponível em Development. |

Os **25 casos de domínio** verificam normalização, limites, IDs, enums, UTC, versão binária, histórico e preservação de estado em alterações inválidas. Os **5 casos de integração** verificam health com relógio controlado, metadados, OpenAPI em Development e ausência do JSON/UI Swagger em Production. Nenhum caso foi ignorado. Não foram executados testes de persistência, autorização, clientes ou infraestrutura, pois tais recursos ainda não existem.

As evidências locais ficam em `artifacts/test-results/day1.trx`, `artifacts/http-smoke.json` e logs `artifacts/api-smoke.*.log`. São artefatos gerados e ignorados no Git; os resultados resumidos acima compõem o registro versionado. Uma nova execução de testes pode recriá-los, sem exigir seu versionamento.

## Pendências técnicas

- Decidir .NET 10 e estratégia oficial de schema no Dia 2.
- Configurar persistência e migrations e validar constraints/rowversion em banco de desenvolvimento escolhido explicitamente.
- Implementar hashing, autenticação e autorização antes dos endpoints de negócio.
- Preparar Docker, Kubernetes e CI/CD nas sprints previstas; verificar toolchains mobile quando o cliente Flutter for iniciado.
