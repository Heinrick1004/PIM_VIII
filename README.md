# TaskFlow — Alunos Tech (PIM VIII)

Aplicação acadêmica para gestão de tarefas internas. O repositório contém uma API ASP.NET Core, uma interface MVC, um aplicativo Flutter para Android e persistência em SQL Server.

## Estrutura

~~~text
src/TaskFlow.Api/             API REST, autenticação JWT e OpenAPI
src/TaskFlow.Web/             Interface web ASP.NET Core MVC
src/TaskFlow.Domain/          Entidades, enums e regras do domínio
src/TaskFlow.Application/     Contratos e abstrações
src/TaskFlow.Infrastructure/  EF Core, migrations e repositórios SQL Server
mobile/                       Aplicativo Flutter/Dart e plataforma Android
database/scripts/             Script SQL de referência; não provisiona o schema
tests/TaskFlow.Tests/          Testes de domínio, HTTP, web e SQL Server
docs/                         Arquitetura, escopo, backlog e evidências
infra/docker/                 Reservado
infra/k8s/                    Reservado
src/TaskFlow.Desktop/         Cliente WinForms C# para API
~~~

API e MVC usam .NET 10; a persistência oficial é SQL Server com migrations EF Core. O cliente móvel usa Flutter, armazenamento seguro do JWT e a API existente. Consulte [arquitetura](docs/arquitetura.md), [modelo de dados](docs/modelo-dados.md) e [escopo](docs/escopo.md).

A API oferece `POST /api/auth/login` e `GET /api/auth/me`; gerenciamento administrativo em `/api/users`; tarefas em `/api/tasks`, incluindo resumo, detalhe, histórico, criação e atualização de status. Alterações concorrentes usam RowVersion e podem retornar HTTP 409. Consulte Swagger em Development para os schemas HTTP completos.

## Requisitos locais

- .NET SDK 10.0.403 (selecionado por `global.json`) e acesso aos pacotes NuGet.
- SQL Server local para executar a aplicação com persistência.
- Para Android: Flutter 3.47.9 ou compatível, Dart 3.13.7, JDK 17 e Android SDK.
- `dotnet-ef` para aplicar migrations em um ambiente novo.

## Banco e segredos

`database/scripts/001_schema.sql` é uma referência histórica. Use as migrations EF Core para criar ou atualizar o schema. A configuração de Development aponta para `TaskFlowDev`; confira `ConnectionStrings:TaskFlow` antes de iniciar a API. Não use `TaskFlowDev` em testes destrutivos.

Para uma demonstração isolada, configure `ConnectionStrings:TaskFlow` para `TaskFlowDemo`, aplique as migrations e mantenha as configurações somente na sessão local. A chave JWT deve ser Base64 com ao menos 256 bits aleatórios e ser fornecida por User Secrets ou pelo ambiente; nunca a versione. Exemplo para gerar uma chave nos User Secrets:

~~~powershell
$bytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Authentication:Jwt:SigningKey" $jwtKey --project src/TaskFlow.Api
dotnet user-secrets set "Authentication:Jwt:Issuer" "TaskFlow.Local" --project src/TaskFlow.Api
dotnet user-secrets set "Authentication:Jwt:Audience" "TaskFlow.Local" --project src/TaskFlow.Api
$rng.Dispose()
[Array]::Clear($bytes, 0, $bytes.Length)
Remove-Variable jwtKey, bytes, rng
~~~

O seeder opcional da conta de demonstração só inicia em Development com `DemoAdmin:Enabled=true`, conexão disponível e catálogo exatamente `TaskFlowDemo`. Ele recebe nome, e-mail e senha de configuração local; a senha deve atender às regras existentes. Um administrador já existente não é sobrescrito, e uma conta com o mesmo e-mail e perfil comum interrompe o startup sem elevação. A API não aplica migrations automaticamente; prepare o schema manualmente antes de habilitar o seeder. O comando `--bootstrap-admin` é exclusivo de `TaskFlowDev` e não deve ser usado para a demonstração.

Exemplo para preparar a sessão de demonstração. Aplique as migrations antes de habilitar o seeder:

~~~powershell
$env:ConnectionStrings__TaskFlow = 'Server=localhost;Database=TaskFlowDemo;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;'
$env:DemoAdmin__Name = 'Administrador de Demonstração'
$env:DemoAdmin__Email = Read-Host 'E-mail da conta de demonstração'
$securePassword = Read-Host 'Senha (12 a 128 caracteres)' -AsSecureString
$env:DemoAdmin__Password = (New-Object System.Net.NetworkCredential('', $securePassword)).Password
dotnet ef database update --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api --context TaskFlowDbContext
$env:DemoAdmin__Enabled = 'true'
dotnet run --project src/TaskFlow.Api --launch-profile http
~~~

Depois de encerrar a API, remova as variáveis de ambiente dessa sessão. Preserve o banco `TaskFlowDemo` e seus dados. Não grave credenciais em arquivos do repositório.

## Executar a API e o site

Na raiz do repositório, com a chave JWT local configurada e a connection string apontando para o banco escolhido:

~~~powershell
dotnet restore TaskFlow.sln
dotnet build TaskFlow.sln --no-restore
dotnet run --project src/TaskFlow.Api --launch-profile http
~~~

O perfil HTTP usa a porta 5046. A documentação OpenAPI fica em `/swagger` e o health check em `/health`. Inicie o MVC em outro terminal:

~~~powershell
dotnet run --project src/TaskFlow.Web --launch-profile http
~~~

Abra `http://localhost:5080`. Em Development, o MVC consome a API em `http://localhost:5046`; fora desse ambiente, use HTTPS e configure chaves persistentes de Data Protection.

## Executar o cliente Desktop

Com a API em execução, execute em outro terminal:

~~~powershell
dotnet run --project src/TaskFlow.Desktop
~~~

A tela permite configurar a URL base (padrão `http://localhost:5046/`), autenticar, consultar tarefas/detalhes/histórico e atualizar status. O JWT permanece apenas na memória do processo; logout ou expiração encerra a sessão.

## Containers e CI

Docker Compose local (API e MVC; usa um SQL Server externo, não cria nem migra banco):

~~~powershell
Copy-Item infra/docker/.env.example infra/docker/.env
# Edite .env com uma connection string de ambiente local e uma chave JWT de teste.
docker compose --env-file infra/docker/.env -f infra/docker/compose.yaml up --build
~~~

A API fica em `http://localhost:5046`, o MVC em `http://localhost:5080`. Não use este perfil Development como implantação pública. O workflow `.github/workflows/ci.yml` executa restore/build/testes .NET seguros e build das imagens em pull requests e pushes para `main`; não faz deploy. Kubernetes e dependências de produção estão descritos em [infra/README.md](infra/README.md). Docker e cluster ainda precisam estar instalados/configurados para validação local.

## Executar o aplicativo Android

Na pasta `mobile`:

~~~powershell
flutter pub get
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
flutter build apk --debug
~~~

Com a API local na porta 5046, uma opção para o emulador Android Studio é encaminhar a porta pelo ADB e executar o app apontando para o loopback do dispositivo:

~~~powershell
adb reverse tcp:5046 tcp:5046
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://127.0.0.1:5046
~~~

O encaminhamento é temporário e deve ser repetido quando necessário. Sem ele, o valor padrão do app é `http://10.0.2.2:5046`. Para detalhes de configuração e rede, consulte [mobile/README.md](mobile/README.md) e [mobile/INTEGRATION.md](mobile/INTEGRATION.md).

O APK de depuração é gerado em `mobile/build/app/outputs/flutter-apk/app-debug.apk`.

## Testes

~~~powershell
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category!=SqlServer"
dotnet test tests/TaskFlow.Tests/TaskFlow.Tests.csproj --filter "Category=SqlServer"
~~~

Os testes SQL usam o fixture `TaskFlowIntegrationTests_<GUID>`, que cria e remove somente o catálogo gerado para a execução. Eles exigem uma conexão SQL Server com permissão para criar e remover bancos; confira o destino antes de executá-los. A validação mobile também inclui `flutter analyze`, `flutter test` e `flutter build apk --debug`.

## Escopo do PIM VIII

O projeto implementa clientes Web, Mobile e Desktop integrados, API modular em C#, backlog e planejamento ágil documentados, migrations, procedures, function, trigger, transações, índices, autorização e concorrência com RowVersion.

Dockerfiles, Compose local, workflow CI e templates Kubernetes estão versionados como artefatos de configuração. O workflow é acionado por pushes para `main`, pull requests e execução manual; seu resultado pode ser acompanhado em Actions. A implantação cloud depende de provedor, cluster, registry, domínio e segredos configurados. Consulte [infraestrutura](infra/README.md). O relatório acadêmico final em DOCX e PDF está em [docs/entrega](docs/entrega/).

O histórico de execução e as limitações conhecidas estão em [docs/ambiente.md](docs/ambiente.md), [docs/diario-desenvolvimento.md](docs/diario-desenvolvimento.md), [docs/backlog.md](docs/backlog.md) e [docs/cronograma.md](docs/cronograma.md). O `Manual.pdf` local permanece fora do versionamento.
\n

## Auditoria final PIM

A matriz atual de banco de dados, gestão ágil, rastreabilidade e lacunas de evidência está em [docs/auditoria-pim.md](docs/auditoria-pim.md). Ela diferencia resultados históricos de verificações da auditoria corrente.
