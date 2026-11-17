# TaskFlow Mobile — integração com a API

Este documento descreve a configuração e as evidências da revisão complementar de 16/11/2026. As consultas SQL desta revisão usaram exclusivamente `TaskFlowDemo`; não foi usado `TaskFlowDev`, não foram criados bancos temporários e nenhum registro de tarefa/histórico foi alterado ou removido. O login de demonstração passou pelo fluxo normal; esse fluxo pode atualizar o hash se detectar senha legada que precise de rehash.

## Banco e inicialização da API

A API não cria nem aplica migrations no startup. O schema de `TaskFlowDemo` já existe e tem quatro migrations registradas. Se for necessário preparar o banco em outro ambiente, confirme primeiro a connection string e o destino; `dotnet ef database update` aplica migrations ao catálogo configurado e pode criar/alterar schema. Não rode esse comando como parte de uma validação somente de leitura.

Para uma conta de demonstração, o seeder só roda em `Development` quando `DemoAdmin:Enabled=true`, a conexão está disponível e o catálogo é exatamente `TaskFlowDemo`. `DemoAdmin:Name`, `DemoAdmin:Email` e `DemoAdmin:Password` devem vir de variáveis de ambiente ou User Secrets locais. Administrador existente não é sobrescrito; e-mail ocupado por conta não administrativa faz o startup falhar sem promovê-la. A API não migra o schema automaticamente. `--bootstrap-admin` é um comando distinto, restrito a `TaskFlowDev`; não o use neste banco.

## Conexão do emulador

O AVD pode acessar o host por `10.0.2.2:5046` quando sua rede virtual estiver ativa. Se não estiver, encaminhe a porta com o AVD conectado:

```powershell
adb reverse tcp:5046 tcp:5046
flutter run --dart-define=TASKFLOW_API_BASE_URL=http://127.0.0.1:5046
```

Não use dispositivo físico nesta validação. O APK de depuração pode ser instalado com `adb install -r build/app/outputs/flutter-apk/app-debug.apk` depois que o AVD estiver online.

## Validações executadas nesta revisão

- Flutter 3.47.9 / Dart 3.13.7: `flutter pub get` passou sem atualizar versões travadas.
- `dart format --output=none --set-exit-if-changed lib test`: 11 arquivos verificados, nenhum alterado.
- `flutter analyze`: sem problemas.
- `flutter test`: 3/3 passaram.
- `flutter build apk --debug`: passou; APK em `build/app/outputs/flutter-apk/app-debug.apk`.
- API em `http://127.0.0.1:5046`: health 200; login demo emitiu JWT; `/api/auth/me`, lista de tarefas (2) e `/api/tasks/summary` (total 2) responderam 200; credencial inválida e resumo sem token retornaram 401. Nenhuma operação de escrita de tarefa/usuário foi chamada; o login pode atualizar o hash se a verificação de senha exigir rehash.
- SQL em `TaskFlowDemo`: consultas diretas somente de leitura encontraram 3 usuários, 2 tarefas, 6 eventos de histórico e 4 migrations; procedure de resumo, function de visibilidade e trigger de proteção existem. A procedure retornou resumo e a function retornou 2 tarefas visíveis; trigger habilitada. Nenhuma escrita SQL, alteração de schema ou migration foi executada.
- MVC: login via API, páginas Painel, Tarefas e Usuários responderam 200; logout encerrou a sessão de teste e rota protegida voltou para `/Account/Login`.
- .NET: restore passou; build passou com 0 avisos/erros em diretório de saída temporário, pois os binários padrão estavam em uso pela API/MVC que permaneceram ativas. A suíte segura não SQL aprovou 65/65.

### Testes de interface não executados

O AVD instalado chama-se `Apple`, mas não iniciou: o Android Emulator informou espaço livre insuficiente no disco C. Havia 6,73 GB livres e o AVD está configurado com partição de dados de 6 GB; o comando terminou sem boot. Depois, `adb devices -l` não listou dispositivos. Não instalei nem abri o app nesta rodada.

Consequentemente, inicialização da UI, login no app, persistência do token em execução, navegação, detalhes, histórico, alteração de status, tratamento visual de HTTP 409, perfil e logout Flutter não foram revalidados nesta rodada. Não foram alteradas tarefas existentes para forçar testes de escrita. A evidência de UI do emulador registrada em revisão anterior é histórica e não deve ser confundida com esta execução.

Os testes automatizados SQL de integração também não foram executados: o fixture cria e exclui um catálogo `TaskFlowIntegrationTests_<GUID>`, operação proibida nesta revisão. RowVersion, conflito 409 e rollback são cobertos por registros de testes históricos, mas não foram repetidos. `TaskFlowDev` não foi conectado.

Para desbloquear a próxima verificação de UI, libere espaço suficiente no disco do AVD e então confira `adb devices -l`; esta revisão não removeu arquivos nem alterou a configuração do emulador.
