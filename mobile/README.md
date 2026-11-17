# TaskFlow Mobile

Procedimento de integracao real com API e SQL Server isolado: [INTEGRATION.md](INTEGRATION.md).

Android build requires JDK 17 (AGP 9.1.0 and Gradle 9.3.1). Install a JDK 17
distribution, then configure `JAVA_HOME` and `PATH` in the PowerShell session:

```powershell
$env:JAVA_HOME = 'C:\Path\to\jdk-17'
$env:Path = "$env:JAVA_HOME\bin;$env:Path"
java -version
```

If using Android Studio, select the same JDK in Settings > Build Tools > Gradle > Gradle JDK.
See the [AGP compatibility table](https://developer.android.com/build/releases/about-agp)
and [Gradle Java compatibility](https://docs.gradle.org/current/userguide/compatibility.html).

Aplicativo Flutter para consultar tarefas da API TaskFlow. A aplicação não usa
banco local: autenticação, tarefas, detalhes e histórico vêm da API existente.
O projeto inclui a plataforma Android para execução em emulador ou aparelho.

## Arquitetura

- `lib/src/config`: URL da API definida no build.
- `lib/src/models`: modelos tipados que espelham os contratos JSON existentes.
- `lib/src/services/api_client.dart`: chamadas HTTP autenticadas para login,
  perfil, tarefas, histórico e atualização de status.
- `lib/src/services/token_store.dart`: armazenamento do JWT com
  `flutter_secure_storage`.
- `lib/src/screens`: login, tarefas, detalhes e perfil.

O login usa `POST /api/auth/login`; a sessão é conferida por `GET /api/auth/me`.
A lista usa `GET /api/tasks?page=1&pageSize=100`. Detalhes e histórico usam
`GET /api/tasks/{id}` e `GET /api/tasks/{id}/history`. A mudança de status usa
`PATCH /api/tasks/{id}/status` com o status numérico e a `rowVersion` atual. Em
HTTP 409, o app recarrega tarefa e histórico antes de permitir nova tentativa.
O botão de atualização só aparece para administrador ou responsável atual,
enquanto a API permanece a autoridade final de autorização.

## Configuração e execução

Instale Flutter estável com suporte ao Android e execute na pasta `mobile`:

```powershell
flutter pub get
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://10.0.2.2:5046
```

O valor padrão `http://10.0.2.2:5046` serve ao emulador Android acessando uma API
na máquina host quando a rede virtual do AVD está ativa. Se o emulador não tiver
rota de rede, encaminhe a porta pelo ADB e use o loopback do dispositivo:

```powershell
adb reverse tcp:5046 tcp:5046
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://127.0.0.1:5046
```

Em um celular físico, use o endereço IP LAN da máquina host, por exemplo:

```powershell
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://192.168.1.20:5046
```

Inicie a API escutando na interface acessível pelo dispositivo, por exemplo:

```powershell
dotnet run --project ..\src\TaskFlow.Api --urls http://0.0.0.0:5046
```

Antes de iniciar, configure `ConnectionStrings__TaskFlow` para um banco isolado
e configure as chaves JWT por User Secrets ou variáveis de ambiente. O perfil
Development da API aponta para `TaskFlowDev`; não o use para validação manual
do app. Para dispositivo físico, o host precisa aceitar conexões na rede local.
O app não inclui credenciais ou chaves de assinatura.

Para outras plataformas/ambientes, passe outro valor a
`TASKFLOW_API_BASE_URL`; não codifique IPs de rede em builds distribuídos.

## Testes e análise

```powershell
flutter analyze
flutter test
```

Os testes locais cobrem a leitura dos contratos JSON e enums. A integração com
login/tarefas exige uma API TaskFlow em execução, um banco isolado com dados de
teste e uma conta de teste válida; os testes unitários não substituem essa
validação ponta a ponta.
