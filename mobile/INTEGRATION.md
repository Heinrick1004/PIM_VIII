# Validacao manual com a API real

Este roteiro usa apenas uma base SQL Server descartavel. Nunca configure a API
ou os comandos abaixo para usar `TaskFlowDev`.

## Preparar o banco e a API

1. Use uma instancia SQL Server de testes e crie um catalogo dedicado, por
   exemplo `TaskFlowMobileIntegrationTests`. Confirme o nome antes de qualquer
   comando que remova o banco.
2. Abra um PowerShell normal e defina a connection string somente na sessao:

   ```powershell
   $env:ConnectionStrings__TaskFlow = 'Server=localhost;Database=TaskFlowMobileIntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True'
   $env:Authentication__Jwt__Issuer = 'TaskFlow.Mobile.Tests'
   $env:Authentication__Jwt__Audience = 'TaskFlow.Mobile.Tests'
   $jwtBytes = New-Object byte[] 32
   [System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
   $env:Authentication__Jwt__SigningKey = [Convert]::ToBase64String($jwtBytes)
   [Array]::Clear($jwtBytes, 0, $jwtBytes.Length)
   ```

   A connection string precisa apontar para o catalogo isolado. Nao coloque
   senha, token ou chave JWT no codigo ou no repositorio.
3. Aplique as migrations com `dotnet ef database update`, usando
   `src/TaskFlow.Infrastructure` como projeto e `src/TaskFlow.Api` como projeto
   de inicializacao. A API nao aplica migrations automaticamente. Execute o
   comando a partir da raiz do repositorio:

   ```powershell
   dotnet ef database update --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api --context TaskFlowDbContext
   ```

4. Crie nesse banco isolado um administrador de teste, um funcionario e uma
   tarefa atribuida ao funcionario. A API nao oferece cadastro anonimo e
   `--bootstrap-admin` e restrito deliberadamente ao banco `TaskFlowDev`. Para
   semear o banco de teste, use `PasswordHasher<Usuario>` e o `TaskFlowDbContext`,
   seguindo os helpers `SeedUserAsync` de
   `tests/TaskFlow.Tests/AuthenticationEndpointTests.cs` e
   `tests/TaskFlow.Tests/TaskEndpointTests.cs`. Guarde a senha descartavel fora
   do repositorio e nao insira senha em texto puro.
5. Na mesma sessao PowerShell, inicie a API na interface de rede:

   ```powershell
   dotnet run --project src/TaskFlow.Api --urls http://0.0.0.0:5046
   ```

## Validar o app

Em outro PowerShell, a partir de `mobile`:

```powershell
flutter pub get
flutter analyze
flutter test
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://10.0.2.2:5046
```

Para aparelho fisico, substitua `10.0.2.2` pelo IP LAN do computador host.
Entre com o funcionario de teste e confirme:

1. Login aceito e perfil carregado por `/api/auth/me`.
2. Lista da tarefa atribuida.
3. Detalhe e historico da tarefa.
4. Atualizacao de status e novo evento no historico.
5. Concorrencia: abra a mesma tarefa em duas sessoes antes de qualquer
   atualizacao; salve uma sessao e depois tente salvar a versao carregada pela
   outra. A segunda operacao deve receber HTTP 409, e o app deve recarregar o
   detalhe e o historico.
6. Logout e novo login.

Ao terminar, encerre a API e remova somente o catalogo temporario criado acima,
depois de confirmar o nome do banco. Preserve `TaskFlowDev`.

## Validacao local mais recente — 16/11/2026

- `flutter pub get`, verificacao de formato em `lib`/`test`, `flutter analyze`,
  `flutter test` (3/3) e build APK Debug foram executados com sucesso.
- Login, perfil, lista, detalhe e historico foram conferidos no emulador Android
  com a API real. O token continuou valido apos reiniciar o processo do app.
- O AVD estava sem rede virtual ativa. O acesso funcionou com o encaminhamento
  ADB abaixo; repita-o quando reiniciar o ADB ou o emulador:

  ```powershell
  adb reverse tcp:5046 tcp:5046
  flutter run --dart-define=TASKFLOW_API_BASE_URL=http://127.0.0.1:5046
  ```

- A API foi validada com consultas a health, login, perfil, tarefas, resumo,
  detalhes e historico, alem de respostas 401/400. As operacoes de tarefas
  foram somente de leitura; nenhuma tarefa foi criada ou alterada durante a
  revisao.
- O site MVC autenticou e carregou Painel, Tarefas e Usuarios.
- Os testes SQL nao foram executados nesta revisao: o fixture cria e remove um
  catalogo `TaskFlowIntegrationTests_<GUID>`, mas a regra desta execucao permite
  usar somente `TaskFlowDemo`. `TaskFlowDev` nao foi conectado nem alterado.
- O Motorola fisico nao foi usado; somente `emulator-5554` apareceu no ADB.

O APK Debug fica em `build/app/outputs/flutter-apk/app-debug.apk`.
