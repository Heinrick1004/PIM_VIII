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
