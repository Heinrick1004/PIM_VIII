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
