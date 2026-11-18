"""Gera os arquivos editáveis e PDF do relatório PIM VIII TaskFlow.

Dependências: Python 3.12 e Google Chrome/Edge instalado. Sem dependências pip.
O DOCX usa campos TOC/SEQ/PAGE do Word com resultados armazenados. O PDF é
renderizado do mesmo conteúdo HTML para manter o arquivo final consultável.
"""
from html import escape
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import subprocess, tempfile, shutil, xml.sax.saxutils as xu

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "entrega"
AUTHOR = "Guilherme Heinrick Araujo da Silva"
RA = "2250728"
CITY_YEAR = "São Paulo\n2026"

SUMMARY = 'Este trabalho apresenta o desenvolvimento do TaskFlow para a Alunos Tech, empresa fictícia que necessita organizar e acompanhar tarefas internas com histórico de alterações. O objetivo foi integrar aplicações, dados e práticas de desenvolvimento em uma solução comum, concebida por incrementos ágeis. A arquitetura reúne uma API REST em ASP.NET Core e clientes Web MVC, Mobile com Flutter e Desktop com Windows Forms, conectados por contratos HTTP. O SQL Server, acessado pelo Entity Framework Core e por migrations, organiza usuários, tarefas e eventos de histórico com chaves e restrições de integridade. A API centraliza autenticação e autorização; transações coordenam gravações e RowVersion implementa concorrência otimista. O projeto inclui function, procedure e trigger, além de consultas paginadas. Como proposta de Cloud Computing e DevOps, foram elaborados Dockerfiles, Compose, workflow de integração contínua no GitHub Actions e manifestos Kubernetes, com verificações de saúde e configuração de escalabilidade. O gerenciamento ágil foi estruturado por backlog priorizado, histórias de usuário, critérios de aceite, sprints planejadas e diário de desenvolvimento. Os registros documentam testes de domínio e integração, validação manual do cliente Mobile e testes HTTP isolados do Desktop. O TaskFlow demonstra a integração dos conteúdos de .NET, banco de dados, aplicações multiplataforma, infraestrutura como código e gestão ágil em resposta a uma necessidade organizacional.'
CONCLUSION = ['O objetivo deste PIM VIII foi alcançado com o desenvolvimento do TaskFlow como solução acadêmica '
 'integrada para o cenário da empresa fictícia Alunos Tech. A plataforma organiza o acompanhamento '
 'de tarefas por interfaces Web, Mobile e Desktop conectadas a uma API comum, oferecendo uma visão '
 'consistente de usuários, atividades e histórico.',
 'A aplicação dos conceitos de .NET estruturou domínio, casos de uso, persistência e serviços HTTP '
 'em camadas. O banco de dados relacional contribui com integridade referencial, consultas '
 'paginadas e objetos programáveis; transações e RowVersion coordenam alterações e concorrência. '
 'Os clientes compartilham autenticação e autorização centralizadas na API, enquanto os registros '
 'de desenvolvimento documentam verificações dos módulos.',
 'Em Cloud Computing e DevOps, foram elaborados artefatos de containerização, integração contínua '
 'e orquestração Kubernetes para descrever a evolução da solução em nuvem. O gerenciamento ágil '
 'conectou requisitos, histórias, critérios de aceite, sprints planejadas, diário e retrospectiva '
 'registrada ao mesmo objetivo. O projeto consolidou conhecimentos de programação, modelagem e '
 'segurança de dados, integração entre plataformas, infraestrutura como código e organização '
 'incremental, demonstrando como esses conteúdos podem compor uma resposta técnica coesa a uma '
 'necessidade empresarial.']

# Cada bloco do desenvolvimento corresponde a uma página de conteúdo. Os
# blocos foram agrupados por argumento/evidência e têm quebras explícitas.
PAGES = [('2 ANÁLISE E ARQUITETURA DA SOLUÇÃO',
  '2.1 Contexto organizacional',
  'A Alunos Tech é a empresa fictícia definida para este PIM VIII. O cenário considerado é o de '
  'uma equipe que distribui demandas internas e precisa acompanhar responsáveis, prazos, '
  'prioridades e decisões. Quando essas informações ficam em canais distintos, torna-se mais '
  'difícil recuperar o estado de uma tarefa e seu histórico. O TaskFlow foi concebido para reunir '
  'esses dados em uma plataforma acessível por diferentes clientes.\n'
  '\n'
  'O escopo funcional contempla usuários com perfis Administrador e Funcionário, tarefas com '
  'título, descrição, prioridade, prazo e status, identificação de criador e responsável e '
  'histórico de alterações. O administrador cadastra e distribui tarefas; o funcionário acompanha '
  'aquelas em que participa e, quando designado, atualiza seu status. A API aplica as regras de '
  'acesso nas operações correspondentes.\n'
  '\n'
  'O backlog relaciona histórias de usuário aos requisitos e critérios de aceite, traduzindo o '
  'cenário organizacional em comportamentos claros para a solução. A plataforma mantém uma fonte '
  'compartilhada de dados e regras: as interfaces apresentam os fluxos adequados a cada '
  'dispositivo, enquanto usuários, tarefas, histórico e autorização permanecem integrados pelo '
  'serviço central.\n'
  '\n'
  'A proposta atende ao escopo acadêmico ao conectar a necessidade de coordenação da Alunos Tech a '
  'funções concretas de consulta, distribuição e acompanhamento. Dessa forma, cada disciplina '
  'contribui para o mesmo objetivo: oferecer uma base organizada para o trabalho cotidiano e '
  'preservar a rastreabilidade das mudanças.'),
 ('2 ANÁLISE E ARQUITETURA DA SOLUÇÃO',
  '2.2 Arquitetura em camadas e fluxo de dados',
  'A solução .NET organiza-se nas camadas Domain, Application, Infrastructure e Api. Domain reúne '
  'entidades e invariantes; Application define casos de uso e contratos; Infrastructure implementa '
  'persistência; e Api expõe os serviços HTTP e configura autenticação e autorização. Essa '
  'separação mantém as regras de negócio independentes do banco e das interfaces.\n'
  '\n'
  'A API REST conecta os clientes ao SQL Server por repositórios e Entity Framework Core. Web MVC '
  'usa serviço HTTP tipado e sessão protegida; Flutter consome endpoints e mantém a sessão em '
  'armazenamento seguro; Desktop WinForms usa serviço HTTP e conserva o token em memória. Nenhum '
  'cliente acessa diretamente o banco.\n'
  '\n'
  'A Figura 1 apresenta o fluxo entre clientes, API, camadas de aplicação e persistência e SQL '
  'Server. A API valida o JWT e aplica permissões a cada requisição. O desenho modular reúne as '
  'operações necessárias ao escopo e mantém no mesmo contexto as gravações de tarefa e histórico. '
  'Os projetos .NET e o cliente Desktop utilizam .NET 10 (MICROSOFT, s.d.-a; MICROSOFT, s.d.-b).'),
 ('3 DESENVOLVIMENTO DAS APLICAÇÕES',
  '3.1 API, autenticação e autorização',
  'A API ASP.NET Core expõe recursos de autenticação, usuários e tarefas. No login, recebe as '
  'credenciais, valida o hash armazenado e emite um token JWT assinado, configurado com parâmetros '
  'de emissor, público e chave fornecidos pelo ambiente. O endpoint /api/auth/me permite consultar '
  'a identidade autenticada. A configuração sensível permanece fora do código-fonte; o seeder de '
  'demonstração é optativo e condicionado ao ambiente Development e às variáveis configuradas.\n'
  '\n'
  'Os contratos da API abrangem criação e edição administrativa, consulta paginada, detalhes, '
  'histórico, resumo e mudança de status. A identidade do autor deriva das claims validadas. As '
  'políticas permitem ao administrador gerenciar tarefas e usuários e restringem ao funcionário os '
  'dados das tarefas em que participa. Detalhes, histórico e mudança de status aplicam as '
  'verificações de vínculo documentadas.\n'
  '\n'
  'As consultas aplicam o escopo de visibilidade antes da contagem e da paginação. Os filtros '
  'incluem status, prioridade, responsável, ausência de responsável e título, com limites para os '
  'parâmetros de página. DTOs definem os dados de entrada e saída, enquanto validações de domínio '
  'e servidor protegem os campos e enumerações. O resumo utiliza procedure parametrizada.\n'
  '\n'
  'A atualização de status transmite a RowVersion recebida pelo cliente. Se outra operação '
  'modificar a tarefa primeiro, a API responde com HTTP 409 e preserva a gravação concorrente já '
  'realizada. Esse mecanismo informa o conflito e permite atualizar os dados antes de uma nova '
  'decisão, evitando sobrescrita silenciosa. Erros de autenticação e autorização são comunicados '
  'por respostas 401 e 403.'),
 ('3 DESENVOLVIMENTO DAS APLICAÇÕES',
  '3.2 Interface Web ASP.NET Core MVC',
  'O cliente Web foi desenvolvido com ASP.NET Core MVC e Razor. Controllers recebem formulários, '
  'chamam um serviço HTTP tipado e encaminham modelos às views. A interface reúne login, painel, '
  'listagem e filtros de tarefas, detalhes e histórico, além de formulários administrativos para '
  'tarefas e usuários. A camada visual utiliza a API e não acessa diretamente o contexto do Entity '
  'Framework Core.\n'
  '\n'
  'Após a autenticação pela API, o MVC guarda o JWT em ticket protegido pelo ASP.NET Core Data '
  'Protection e utiliza cookie de sessão. O token não é exposto em campos do formulário nem ao '
  'JavaScript. O logout encerra a sessão local, e formulários que alteram dados aplicam proteção '
  'antiforgery. As configurações para ambiente externo preveem HTTPS e persistência segura do key '
  'ring entre reinicializações e réplicas.\n'
  '\n'
  'As respostas da API são tratadas de acordo com seu significado: sessão expirada retorna ao '
  'login, falta de permissão apresenta acesso negado, e falhas de conexão ou conflitos recebem '
  'mensagens próprias. Registros de desenvolvimento incluem testes de controllers e renderização '
  'em ambiente de preview, além da revisão da navegação e de diferentes larguras de tela.\n'
  '\n'
  'O endereço da API é configurável por ambiente. O perfil local permite HTTP para '
  'desenvolvimento; a configuração externa utiliza HTTPS, origem permitida e chaves persistentes. '
  'Assim, o cliente Web compartilha o contrato da API e mantém a responsabilidade pela '
  'apresentação, enquanto autenticação e autorização permanecem centralizadas no serviço.'),
 ('3 DESENVOLVIMENTO DAS APLICAÇÕES',
  '3.3 Cliente Mobile Flutter',
  'O aplicativo Mobile foi desenvolvido em Dart com Flutter para Android, oferecendo acesso às '
  'tarefas pela mesma API utilizada pelos demais clientes. Seus fluxos incluem autenticação, '
  'gerenciamento de sessão, consulta, visualização de detalhes e atualização de status. A '
  'integração usa HTTP, endereço configurável e modelos próprios para as respostas, sem acesso '
  'direto ao SQL Server.\n'
  '\n'
  'O token da sessão é mantido em armazenamento seguro de plataforma. A API verifica assinatura, '
  'validade, perfil e vínculo com a tarefa em cada operação; os controles apresentados na '
  'interface orientam a navegação e complementam as respostas de autorização do servidor. O '
  'tratamento de falhas de rede e das respostas 401, 403 e 409 torna explícito o resultado das '
  'solicitações.\n'
  '\n'
  'O diário de desenvolvimento registra uma validação manual no emulador Android com login, '
  'perfil, listagem, detalhes, histórico e restauração de sessão por adb reverse. Essa validação '
  'documentada integra as evidências do cliente Mobile e demonstra seu fluxo de comunicação com a '
  'API no ambiente utilizado.\n'
  '\n'
  'O aplicativo amplia os pontos de acesso ao TaskFlow sem duplicar regras de negócio. A '
  'compatibilidade entre modelos, rotas e respostas é mantida pelo contrato HTTP compartilhado, '
  'enquanto a interface móvel apresenta as ações pertinentes à rotina de consulta e acompanhamento '
  'de tarefas.'),
 ('3 DESENVOLVIMENTO DAS APLICAÇÕES',
  '3.4 Cliente Desktop WinForms',
  'O cliente Desktop utiliza Windows Forms com .NET 10 e mantém a integração com a API por um '
  'serviço HTTP próprio. A janela organiza login, lista de tarefas, detalhes, histórico e mudança '
  'de status. Modelos de API representam as respostas, e o serviço encapsula rotas, cabeçalhos '
  'bearer e interpretação dos códigos HTTP. O token permanece em memória durante a sessão.\n'
  '\n'
  'No login, a aplicação inicia a sessão após receber resposta válida da API. Respostas 401 e 403 '
  'informam, respectivamente, que a autenticação não foi aceita ou que a operação não está '
  'autorizada. Falhas de conexão comunicam a indisponibilidade do serviço, e HTTP 409 indica '
  'conflito de versão para que os dados sejam atualizados. O logout limpa a sessão e os dados '
  'apresentados.\n'
  '\n'
  'A lista exibe as tarefas disponíveis ao usuário autenticado; a seleção consulta detalhes e '
  'histórico pelo identificador. A atualização de status envia o valor Base64 de RowVersion '
  'recebido da API. Assim, o cliente Desktop aplica o mesmo controle de concorrência e as mesmas '
  'permissões que os outros pontos de acesso, sem conexão direta ao banco.\n'
  '\n'
  'Os registros de implementação documentam a compilação do cliente sem avisos ou erros e sete '
  'testes HTTP isolados sobre a interpretação de respostas da API. A separação entre tela, serviço '
  'HTTP e DTOs mantém o código desacoplado da persistência e permite evoluir a interface '
  'preservando os contratos da solução.'),
 ('4 PROGRAMAÇÃO DE BANCO DE DADOS',
  '4.1 Modelo relacional e integridade',
  'O SQL Server persiste três entidades centrais: Usuarios, Tarefas e HistoricoTarefas. Usuario '
  'contém identificador, nome, e-mail, hash de senha e perfil. Tarefa reúne título, descrição, '
  'status, prioridade, datas UTC, criador, responsável opcional e RowVersion. HistoricoTarefa '
  'registra tarefa, autor, ação, data e descrição. As relações permitem que usuários criem e '
  'acompanhem tarefas com vários eventos associados.\n'
  '\n'
  'As configurações do Entity Framework Core definem tipos e limites compatíveis com as regras de '
  'domínio. E-mail possui índice único com comparação sem distinção entre maiúsculas e minúsculas; '
  'constraints CHECK limitam valores de enum e campos obrigatórios. GUIDs identificam registros, '
  'datas usam UTC e datetime2, e chaves estrangeiras preservam integridade referencial sem '
  'exclusão em cascata. Migrations versionam a evolução do schema.\n'
  '\n'
  'A integridade também é aplicada na camada de domínio, que valida e-mail e limites de texto, '
  'normaliza entradas e verifica datas. A aplicação calcula o hash da senha antes da persistência '
  'e não o inclui nos DTOs. O banco protege a consistência estrutural; a API aplica as regras de '
  'acesso aos dados.\n'
  '\n'
  'A unidade de trabalho coordena as gravações de tarefa e histórico. A criação e seu primeiro '
  'evento são confirmados na mesma transação; alterações efetivas geram eventos associados, e '
  'operações sem mudança de valor evitam duplicidade. O histórico é disponibilizado em consulta '
  'paginada, o que facilita acompanhar a evolução da atividade.'),
 ('4 PROGRAMAÇÃO DE BANCO DE DADOS',
  '4.2 Função, procedure, trigger e transações',
  'A migration AddTaskFlowProgrammability define dbo.fn_TarefasVisiveis, dbo.usp_TarefasResumo e '
  'dbo.TR_Tarefas_ProtegeDadosCriacao. A função tabular retorna tarefas criadas pelo usuário ou '
  'atribuídas a ele sem duplicar registros. A procedure recebe identidade e perfil administrativo, '
  'calcula totais por status e aplica a função para limitar o resumo do funcionário. A listagem '
  'principal usa consultas LINQ traduzidas para SQL, e o resumo consome a procedure '
  'parametrizada.\n'
  '\n'
  'A trigger AFTER UPDATE compara as tabelas inserted e deleted e protege CriadorId e CriadaEmUtc '
  'contra alterações. O erro 51001 identifica a tentativa de modificar esses dados de criação. A '
  'regra é aplicada também a atualizações com múltiplas linhas; os demais campos permanecem '
  'editáveis segundo as operações previstas. A trigger complementa as validações da aplicação e '
  'não substitui o registro de histórico.\n'
  '\n'
  'As transações da infraestrutura agrupam a gravação de tarefas e seus eventos. Em caso de falha '
  'intermediária, rollback mantém a consistência entre os registros; na atualização concorrente, o '
  'evento da operação rejeitada não é confirmado. As migrations criam e removem function, '
  'procedure e trigger de forma rastreável, respeitando a ordem de dependência (MICROSOFT, '
  's.d.-c).\n'
  '\n'
  'Os registros de 13/11/2026 documentam 123 testes aprovados na suíte integrada, incluindo '
  'cenários de function, procedure, trigger, transação, RowVersion e chamadas HTTP com SQL Server '
  'isolado. Esse resultado histórico registra a verificação dos objetos programáveis e dos fluxos '
  'de persistência no ciclo de desenvolvimento.'),
 ('4 PROGRAMAÇÃO DE BANCO DE DADOS',
  '4.3 Consultas, desempenho e concorrência',
  'As consultas de tarefas, usuários e histórico aplicam filtros no servidor, contam resultados '
  'antes da paginação e usam Skip/Take com ordenação determinística. Rotas de leitura utilizam '
  'AsNoTracking quando as entidades não serão editadas no contexto. A paginação limita o volume de '
  'dados materializado por solicitação; os parâmetros de tarefas e histórico aceitam até 100 itens '
  'por página. A documentação do Entity Framework Core recomenda consultas ajustadas aos dados '
  'necessários e à execução real (MICROSOFT, s.d.-d).\n'
  '\n'
  'O modelo inclui o índice composto IX_Tarefas_Status_ResponsavelId e índices para chaves '
  'primárias, estrangeiras e e-mail. A ordenação por data e identificador fornece desempate '
  'estável. O filtro por substring atende à busca de título prevista no escopo; sua eficiência em '
  'diferentes volumes pode ser avaliada por plano de execução e carga representativa antes de '
  'selecionar estratégias adicionais.\n'
  '\n'
  'RowVersion é um token binário de oito bytes gerado pelo SQL Server. O EF Core compara a versão '
  'original enviada pelo cliente com o valor armazenado; se outra transação tiver vencido, a API '
  'retorna HTTP 409. O token trafega em Base64 no contrato HTTP e representa concorrência '
  'otimista, não um contador de negócio (MICROSOFT, s.d.-e; MICROSOFT, s.d.-f).\n'
  '\n'
  'A identidade e as permissões são verificadas antes das consultas. Comandos SQL utilizam '
  'parâmetros tipados, e a procedure é chamada como stored procedure. Índices, paginação, filtros '
  'e RowVersion formam a base de consistência e acesso aos dados implementada no projeto; a '
  'avaliação quantitativa de capacidade depende de medições com carga representativa.'),
 ('5 CLOUD COMPUTING E DEVOPS',
  '5.1 Topologia proposta e conteinerização',
  'A arquitetura proposta para Cloud Computing separa API e MVC em imagens .NET que podem ser '
  'executadas em Kubernetes gerenciado atrás de Ingress com TLS. O SQL Server permanece como '
  'serviço externo acessível por rede privada. Conexão, emissor, público e chave JWT são '
  'parâmetros do ambiente; o repositório contém exemplos de configuração sem credenciais reais. '
  'Logs estruturados são preparados para integração com os mecanismos de coleta do provedor.\n'
  '\n'
  'Os Dockerfiles seguem estratégia multi-stage: SDK para restauração e compilação e runtime para '
  'execução da aplicação publicada, reduzindo dependências na imagem final (DOCKER, s.d.). Os '
  'containers definem usuário não privilegiado, portas e verificações de saúde. O Compose descreve '
  'a execução local de API e MVC e utiliza uma instância SQL Server configurada no ambiente.\n'
  '\n'
  'Os endpoints /health e /ready oferecem sinais distintos. O primeiro verifica a disponibilidade '
  'do processo; o segundo verifica a dependência do banco e informa quando a aplicação está pronta '
  'para receber solicitações. A configuração do MVC prevê persistência protegida do key ring Data '
  'Protection para uso entre reinicializações e réplicas.\n'
  '\n'
  'Esses Dockerfiles, o Compose e a topologia de serviços constituem artefatos declarativos de '
  'implantação preparados no projeto. Sua aplicação em nuvem utiliza os parâmetros, recursos de '
  'rede, armazenamento e segredos definidos pelo ambiente provedor, mantendo separados o código da '
  'aplicação e a configuração operacional.'),
 ('5 CLOUD COMPUTING E DEVOPS',
  '5.2 CI, Kubernetes e escalabilidade',
  'O workflow GitHub Actions organiza restore, build e testes .NET em Windows, compatível com o '
  'cliente WinForms, e a construção das imagens da API e do Web em Linux. A estrutura segue as '
  'etapas recomendadas para automação de aplicações .NET e conecta integração contínua aos '
  'arquivos versionados do projeto (GITHUB, s.d.).\n'
  '\n'
  'Os manifestos Kubernetes definem namespace, ConfigMap, serviços, Ingress TLS, PVC e deployments '
  'para API e Web. Contexto de segurança e solicitações de CPU e memória descrevem os recursos de '
  'execução. Readiness e liveness usam endpoints distintos, e os Horizontal Pod Autoscalers '
  'definem parâmetros para escala horizontal conforme utilização de CPU, com suporte de métricas '
  'do cluster (KUBERNETES, s.d.-a; KUBERNETES, s.d.-b).\n'
  '\n'
  'Para compor o ambiente, os manifestos preveem imagens em registry, secrets gerenciados, domínio '
  'e certificado TLS, Ingress class e StorageClass compatível com o PVC. As chaves Data '
  'Protection, acesso ao SQL Server e políticas de rede também integram a configuração de '
  'segurança. A automação de migrations permanece supervisionada e vinculada ao ciclo de '
  'implantação.\n'
  '\n'
  'O conjunto descreve em código a integração contínua e a topologia de orquestração planejadas '
  'para o TaskFlow. Com os recursos do provedor e do cluster configurados, esses artefatos '
  'oferecem uma base para construir, observar e escalar os serviços; a expansão de réplicas é '
  'considerada junto à capacidade do banco, à sessão compartilhada e à segurança das credenciais.'),
 ('6 GERENCIAMENTO DE PROJETOS ÁGIL',
  '6.1 Backlog e critérios de aceite',
  'O gerenciamento do projeto foi registrado em Product Backlog, cronograma e diário de '
  'desenvolvimento. O backlog reúne as histórias US01 a US13, prioridades P0/P1/P2, estimativas '
  'relativas em pontos, relação com requisitos e critérios de aceite. As estimativas apoiam a '
  'comparação de esforço entre histórias e não representam horas executadas.\n'
  '\n'
  'A priorização organiza o trabalho desde os fundamentos de domínio, persistência e autenticação '
  'até as interfaces e a infraestrutura. As histórias representam necessidades de usuários, '
  'administradores, desenvolvedores e mantenedores e conectam esses objetivos aos módulos '
  'implementados. Critérios como respostas 401 e 403, paginação, registro transacional de eventos '
  'e conflito HTTP 409 tornam os resultados verificáveis.\n'
  '\n'
  'O backlog relaciona IDs de requisitos, critérios de aceite e atividades documentadas, enquanto '
  'o diário registra decisões, progresso e ajustes de prioridade. Esse conjunto oferece '
  'rastreabilidade entre o problema organizacional, os incrementos técnicos e as verificações '
  'descritas no trabalho.\n'
  '\n'
  'As práticas ágeis adotadas apoiam a organização incremental do TaskFlow: priorizar valor, '
  'decompor entregas em histórias compreensíveis e acompanhar sua evolução em ciclos planejados. '
  'Assim, o gerenciamento orienta API, clientes, banco e infraestrutura para a mesma entrega '
  'integrada.'),
 ('6 GERENCIAMENTO DE PROJETOS ÁGIL',
  '6.2 Sprints, acompanhamento e retrospectiva',
  'O planejamento agrupou as atividades em três sprints: S1, de 09 a 13/11; S2, de 14 a 18/11; e '
  'S3, planejada de 19 a 22/11 de 2026. As datas compõem o cronograma interno do projeto. O diário '
  'de desenvolvimento registra as entregas e as prioridades acompanhadas ao longo dos ciclos.\n'
  '\n'
  'A Sprint 1 concentrou a estrutura .NET, persistência, autenticação, usuários e criação de '
  'tarefas com histórico. O registro de retrospectiva de 10/11 documenta a revisão das histórias '
  'iniciais, os pontos observados pela equipe e ações para o ciclo seguinte. Essa prática apoiou a '
  'revisão do escopo e dos critérios das próximas entregas.\n'
  '\n'
  'Na Sprint 2, o núcleo de consulta e atualização de tarefas foi ampliado com filtros, paginação, '
  'controle RowVersion e integração dos clientes Web com a API e o banco de dados. Os registros de '
  'desenvolvimento também associam a essa etapa verificações de visibilidade, conflitos '
  'concorrentes e rollback. As atividades Mobile, Desktop e infraestrutura compõem os incrementos '
  'de integração previstos para o projeto.\n'
  '\n'
  'O backlog, o cronograma, o diário e o registro de retrospectiva conectam planejamento, '
  'implementação e aprendizado. Essa organização ágil acompanha o desenvolvimento multidisciplinar '
  'em torno de um produto único e favorece ajustes de prioridade a partir das atividades '
  'documentadas.'),
 ('7 TESTES E RESULTADOS',
  '7.1 Evidências de validação',
  'A estratégia de verificação reúne testes de domínio, testes HTTP e integração com SQL Server. '
  'Os testes de domínio exercitam invariantes sem persistência; testes HTTP avaliam contratos e '
  'middleware; e a integração utiliza fixtures SQL isoladas para migrations e operações de dados. '
  'Cada grupo observa aspectos diferentes da aplicação.\n'
  '\n'
  'Os registros de desenvolvimento documentam 27 testes de domínio aprovados em 17/11/2026. Em '
  '13/11/2026, a suíte integrada registrou 123 testes aprovados em cenários que abrangem function, '
  'procedure, trigger, transações, concorrência RowVersion e API com SQL Server isolado. As datas '
  'identificam os ciclos em que essas execuções foram registradas.\n'
  '\n'
  'Para o cliente Desktop, estão registrados build sem avisos ou erros e sete testes HTTP isolados '
  'para interpretação das respostas da API. No cliente Web, os registros anteriores incluem testes '
  'de controller e renderização e revisão responsiva. Para Mobile, o diário descreve validação '
  'manual no emulador Android dos fluxos de login, consulta, detalhe, histórico e sessão.\n'
  '\n'
  'Essas verificações acompanham os módulos que compõem o TaskFlow e demonstram a aplicação dos '
  'mecanismos de qualidade ao domínio, aos contratos HTTP, à persistência e às interfaces. Os '
  'resultados apresentados identificam a modalidade e o registro correspondente, permitindo '
  'relacionar a evidência ao componente avaliado.'),
 ('7 TESTES E RESULTADOS',
  '7.2 Síntese dos resultados e integração',
  'O TaskFlow integra uma API central, clientes Web, Mobile e Desktop e um modelo relacional '
  'versionado por migrations. A API compartilha contratos e aplica autenticação e autorização; o '
  'banco relaciona usuários, tarefas e eventos de histórico com integridade, transações e '
  'concorrência otimista. As interfaces oferecem formas complementares de acessar o mesmo '
  'serviço.\n'
  '\n'
  'A contribuição de programação de banco inclui modelo relacional, índices, constraints, '
  'function, procedure, trigger e controle de concorrência. A aplicação .NET organiza domínio, '
  'casos de uso, persistência, API REST, MVC e cliente WinForms; o cliente Flutter amplia o acesso '
  'por dispositivos Android. Cloud Computing e DevOps relacionam-se à solução por meio de '
  'configurações de containers, integração contínua e Kubernetes elaboradas para descrever a '
  'infraestrutura proposta.\n'
  '\n'
  'O gerenciamento ágil conecta backlog, critérios de aceite, sprints planejadas, diário e '
  'retrospectiva registrada às entregas do mesmo produto. Em conjunto, as disciplinas tratam da '
  'necessidade da Alunos Tech de distribuir tarefas e acompanhar alterações com acesso '
  'controlado.\n'
  '\n'
  'Os resultados consistem em uma base acadêmica integrada, documentada por módulos e acompanhada '
  'por registros de verificação. O projeto articula conceitos de desenvolvimento, dados, '
  'integração entre plataformas e automação, apresentando os artefatos de infraestrutura como '
  'configuração declarativa para uma evolução em nuvem.')]

INTRO = ['Organizações que acompanham demandas em mensagens e planilhas podem ter dificuldade para '
 'identificar o estado atual de cada atividade, acompanhar mudanças e distribuir '
 'responsabilidades. Esse cenário orienta o Projeto Integrado Multidisciplinar VIII: a empresa '
 'fictícia Alunos Tech necessita centralizar tarefas internas e registrar seu histórico. A questão '
 'que conduz o trabalho é como integrar aplicações e persistência de dados para oferecer consulta '
 'e atualização de tarefas com controle de acesso.',
 'O objetivo geral é desenvolver e documentar o TaskFlow como uma solução acadêmica integrada, '
 'articulando programação .NET, banco de dados, Cloud Computing e DevOps e gerenciamento ágil. '
 'Para isso, foram estruturados uma API e clientes Web, Mobile e Desktop; um modelo relacional '
 'para usuários, tarefas e histórico; mecanismos de autenticação, autorização e concorrência; e '
 'configurações declarativas de infraestrutura e automação.',
 'A metodologia foi incremental e guiada por requisitos funcionais e não funcionais, histórias de '
 'usuário e critérios de aceite. O desenvolvimento organizou regras de domínio, persistência e '
 'serviços HTTP, conectou os clientes à API e registrou atividades e verificações. A documentação '
 'relaciona a estrutura do código, as migrations, os testes registrados e os artefatos de '
 'infraestrutura, distinguindo implementação de configurações propostas para evolução em nuvem.',
 'O texto apresenta o contexto e a arquitetura integrada, o desenvolvimento dos clientes, a '
 'modelagem e a programação do banco de dados, a proposta de Cloud Computing e DevOps, a '
 'organização ágil e os resultados documentados. A conclusão reúne as contribuições técnicas do '
 'TaskFlow e a integração entre as disciplinas em torno da necessidade de organizar e acompanhar '
 'tarefas.']

ABSTRACT = ('This paper presents the development of TaskFlow for Alunos Tech, a fictional company that needs '
 'to organize and track internal tasks and their change history. The goal was to integrate '
 'applications, data, and development practices in a shared solution designed through agile '
 'increments. The architecture combines an ASP.NET Core REST API with MVC web, Flutter mobile, and '
 'Windows Forms desktop clients connected through HTTP contracts. SQL Server, accessed through '
 'Entity Framework Core and migrations, stores users, tasks, and history events with keys and '
 'integrity constraints. The API centralizes authentication and authorization; transactions '
 'coordinate writes, while RowVersion implements optimistic concurrency. The project includes a '
 'function, a stored procedure, a trigger, and paginated queries. As a Cloud Computing and DevOps '
 'proposal, it includes prepared Dockerfiles, Compose, a GitHub Actions continuous integration '
 'workflow, and Kubernetes manifests with health checks and scaling configuration. Agile project '
 'management was organized through a prioritized backlog, user stories, acceptance criteria, '
 'planned sprints, and a development log. Project records document domain and integration tests, '
 'manual validation of the mobile client, and isolated HTTP tests for the desktop client. TaskFlow '
 'demonstrates how .NET, database development, multiplatform applications, infrastructure as code, '
 'and agile management can be integrated to address an organizational need.')

REFS = [
"DOCKER. Multi-stage builds. Docker Documentation, s.d. Disponível em: https://docs.docker.com/build/building/multi-stage/. Acesso em: 17 nov. 2026.",
"GITHUB. Building and testing .NET. GitHub Docs, s.d. Disponível em: https://docs.github.com/en/actions/tutorials/build-and-test-code/net. Acesso em: 17 nov. 2026.",
"KUBERNETES. Liveness, readiness, and startup probes. Kubernetes Documentation, s.d.-a. Disponível em: https://kubernetes.io/docs/concepts/workloads/pods/probes/. Acesso em: 17 nov. 2026.",
"KUBERNETES. Horizontal Pod Autoscaling. Kubernetes Documentation, s.d.-b. Disponível em: https://kubernetes.io/docs/concepts/workloads/autoscaling/horizontal-pod-autoscale/. Acesso em: 17 nov. 2026.",
"MICROSOFT. ASP.NET Core MVC overview. Microsoft Learn, s.d.-a. Disponível em: https://learn.microsoft.com/en-us/aspnet/core/mvc/overview?view=aspnetcore-10.0. Acesso em: 17 nov. 2026.",
"MICROSOFT. Object-oriented programming (C#). Microsoft Learn, s.d.-b. Disponível em: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/oop. Acesso em: 17 nov. 2026.",
"MICROSOFT. Using transactions. EF Core, Microsoft Learn, s.d.-c. Disponível em: https://learn.microsoft.com/en-us/ef/core/saving/transactions. Acesso em: 17 nov. 2026.",
"MICROSOFT. Efficient querying. EF Core, Microsoft Learn, s.d.-d. Disponível em: https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying. Acesso em: 17 nov. 2026.",
"MICROSOFT. rowversion (Transact-SQL). SQL Server, Microsoft Learn, s.d.-e. Disponível em: https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql?view=sql-server-ver15. Acesso em: 17 nov. 2026.",
"MICROSOFT. Handling concurrency conflicts. EF Core, Microsoft Learn, s.d.-f. Disponível em: https://learn.microsoft.com/en-us/ef/core/saving/concurrency. Acesso em: 17 nov. 2026.",
"SCHWABER, Ken; SUTHERLAND, Jeff. The Scrum Guide: the definitive guide to Scrum. Nov. 2020. Disponível em: https://scrumguides.org/scrum-guide.html. Acesso em: 17 nov. 2026.",
"UNIVERSIDADE PAULISTA. Manual do Projeto Integrado Multidisciplinar VIII. São Paulo: UNIP, [s.d.]. Documento fornecido para orientação do PIM VIII.",
"TASKFLOW. Repositório do projeto: código-fonte, migrations e documentação técnica. GitHub, 2026. Disponível em: https://github.com/Heinrick1004/PIM_VIII. Acesso em: 17 nov. 2026.",
]

def field(instr, result=""):
    return (f'<w:fldChar w:fldCharType="begin"/><w:instrText xml:space="preserve"> {xu.escape(instr)} </w:instrText>'
            f'<w:fldChar w:fldCharType="separate"/><w:t>{xu.escape(result)}</w:t><w:fldChar w:fldCharType="end"/>')

def fit_text(text, limit=2150):
    """Keep every authored paragraph and require it to fit the planned page."""
    if len(text) > limit:
        raise ValueError(f"Section has {len(text)} characters; page limit is {limit}.")
    return text

def run(text, bold=False, size=24, italic=False):
    props = f'<w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="{size}"/>'
    if bold: props += '<w:b/>'
    if italic: props += '<w:i/>'
    props += '</w:rPr>'
    return f'<w:r>{props}<w:t xml:space="preserve">{xu.escape(text)}</w:t></w:r>'

def para(text="", style=None, align=None, first=True, bold=False, size=24, before=0, after=120, spacing=360, page=False):
    ppr = '<w:pPr>'
    if style: ppr += f'<w:pStyle w:val="{style}"/>'
    if align: ppr += f'<w:jc w:val="{align}"/>'
    if first: ppr += '<w:ind w:firstLine="851"/>'
    ppr += f'<w:spacing w:before="{before}" w:after="{after}" w:line="{spacing}" w:lineRule="auto"/>'
    if page: ppr += '<w:pageBreakBefore/>'
    ppr += '</w:pPr>'
    return f'<w:p>{ppr}{run(text,bold,size)}</w:p>'

def field_para(instr, result, align="left"):
    return f'<w:p><w:pPr><w:jc w:val="{align}"/><w:spacing w:after="0"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="24"/></w:rPr>{field(instr,result)}</w:r></w:p>'

def xml_doc():
    body=[]
    def add(t="",style=None,**kw): body.append(para(t,style,**kw))
    def breakpage(): body.append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
    # Capa
    add("UNIVERSIDADE PAULISTA – UNIP EaD",align="center",first=False,bold=True,after=120)
    add("PROJETO INTEGRADO MULTIDISCIPLINAR VIII",align="center",first=False,bold=True)
    for _ in range(5): add("",first=False,after=0)
    add(AUTHOR.upper()+" – RA "+RA,align="center",first=False,bold=True)
    for _ in range(4): add("",first=False,after=0)
    add("TASKFLOW: SISTEMA INTEGRADO PARA GESTÃO DE TAREFAS DA ALUNOS TECH",align="center",first=False,bold=True)
    for _ in range(7): add("",first=False,after=0)
    for line in CITY_YEAR.splitlines(): add(line,align="center",first=False)
    breakpage()
    # Folha de rosto
    add(AUTHOR.upper()+" – RA "+RA,align="center",first=False,bold=True)
    for _ in range(4): add("",first=False,after=0)
    add("TASKFLOW: SISTEMA INTEGRADO PARA GESTÃO DE TAREFAS DA ALUNOS TECH",align="center",first=False,bold=True)
    for _ in range(4): add("",first=False,after=0)
    add("Projeto Integrado Multidisciplinar apresentado à Universidade Paulista – UNIP EaD, como requisito acadêmico do curso Superior de Tecnologia em Análise e Desenvolvimento de Sistemas, no âmbito do Projeto Integrado Multidisciplinar VIII.",align="right",first=False,spacing=240,after=0)
    for _ in range(6): add("",first=False,after=0)
    for line in CITY_YEAR.splitlines(): add(line,align="center",first=False)
    breakpage()
    add("RESUMO",align="center",first=False,bold=True,size=28,after=240)
    summary = SUMMARY
    add(summary,first=False,spacing=240,after=120)
    add("Palavras-chave: gestão de tarefas; API; SQL Server; .NET; DevOps.",first=False,spacing=240)
    breakpage()
    add("ABSTRACT",align="center",first=False,bold=True,size=28,after=240)
    add(ABSTRACT,first=False,spacing=240)
    add("Keywords: task management; API; SQL Server; .NET; DevOps.",first=False,spacing=240)
    breakpage()
    add("LISTA DE ILUSTRAÇÕES",align="center",first=False,bold=True,size=28,after=240)
    body.append(field_para('TOC \\h \\z \\c "Figura"',"Figura 1 – Arquitetura lógica do TaskFlow .................................................. 9"))
    breakpage()
    add("SUMÁRIO",align="center",first=False,bold=True,size=28,after=240)
    toc=[("1 INTRODUÇÃO",7),("2 ANÁLISE E ARQUITETURA DA SOLUÇÃO",8),("2.1 Contexto organizacional",8),("2.2 Arquitetura em camadas e fluxo de dados",9),("3 DESENVOLVIMENTO DAS APLICAÇÕES",10),("3.1 API, autenticação e autorização",10),("3.2 Interface Web ASP.NET Core MVC",11),("3.3 Cliente Mobile Flutter",12),("3.4 Cliente Desktop WinForms",13),("4 PROGRAMAÇÃO DE BANCO DE DADOS",14),("4.1 Modelo relacional e integridade",14),("4.2 Função, procedure, trigger e transações",15),("4.3 Consultas, desempenho e concorrência",16),("5 CLOUD COMPUTING E DEVOPS",17),("5.1 Topologia proposta e conteinerização",17),("5.2 CI, Kubernetes e escalabilidade",18),("6 GERENCIAMENTO DE PROJETOS ÁGIL",19),("6.1 Backlog e critérios de aceite",19),("6.2 Sprints, acompanhamento e retrospectiva",20),("7 TESTES E RESULTADOS",21),("7.1 Evidências de validação",21),("7.2 Síntese dos resultados e integração",22),("8 CONCLUSÃO",23),("REFERÊNCIAS",24)]
    body.append('<w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> TOC \\o "1-2" \\h \\z \\u </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r></w:p>')
    for label,n in toc:
        body.append(para(f"{label} ........................................................................ {n}",first=False,spacing=240,after=0,size=20))
    body.append('<w:p><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>')
    breakpage()
    # Introdução, page 7; all pre-text pages are unnumbered.
    body.append(field_para('PAGE','7',"right"))
    add("1 INTRODUÇÃO","Heading1",first=False)
    for t in INTRO: add(t,first=True,spacing=360)
    # Illustration counted from the sample's page structure: follows introduction.
    breakpage()
    body.append(field_para('PAGE','8',"right"))
    # 15 development pages, 8–22.
    prior_chapter = None
    for idx,(chapter,sub,text) in enumerate(PAGES):
        if idx:
            breakpage(); body.append(field_para('PAGE',str(9+idx),"right"))
        add(chapter,"Heading1" if chapter != prior_chapter else None,first=False,bold=(chapter == prior_chapter),size=28 if chapter != prior_chapter else 24)
        add(sub,"Heading2",first=False)
        for para_text in fit_text(text,1700 if idx==1 else 2150).split("\n\n"):
            add(para_text,first=True,spacing=360)
        if idx==1:
            # Figure caption field with a cache that Word can refresh.
            body.append('<w:p><w:pPr><w:pStyle w:val="Caption"/><w:jc w:val="center"/><w:spacing w:line="240" w:lineRule="auto"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="20"/></w:rPr><w:t>Figura </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> SEQ Figura \\* ARABIC </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r><w:r><w:t> – Arquitetura lógica do TaskFlow</w:t></w:r></w:p>')
            body.append(para("Clientes Web, Mobile e Desktop → API ASP.NET Core → Application/Domain → Infrastructure/EF Core → SQL Server",align="center",first=False,size=20,spacing=240,after=0))
            body.append(para("Fonte: elaborado pelo autor com base no código do projeto (2026).",align="center",first=False,size=20,spacing=240))
        prior_chapter = chapter
    breakpage(); body.append(field_para('PAGE','23',"right")); add("8 CONCLUSÃO","Heading1",first=False)
    for p in CONCLUSION:
        add(p,first=True,spacing=360)
    breakpage(); body.append(field_para('PAGE','24',"right")); add("REFERÊNCIAS","Heading1",first=False)
    for ref in REFS: add(ref,first=False,spacing=240,after=240)
    return '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>'+''.join(body)+'<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1701" w:right="1134" w:bottom="1134" w:left="1701" w:header="567" w:footer="567" w:gutter="0"/><w:cols w:space="720"/></w:sectPr></w:body></w:document>'

def write_docx():
    doc=xml_doc()
    styles='''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
    <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="24"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="360" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
    <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
    <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="240" w:after="240"/></w:pPr><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style>
    <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="180" w:after="180"/></w:pPr><w:rPr><w:b/><w:sz w:val="24"/></w:rPr></w:style>
    <w:style w:type="paragraph" w:styleId="Caption"><w:name w:val="caption"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/></w:pPr><w:rPr><w:sz w:val="20"/></w:rPr></w:style></w:styles>'''
    settings='<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:updateFields w:val="true"/></w:settings>'
    types='''<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/></Types>'''
    rels='''<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'''
    docrels='''<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/></Relationships>'''
    path=OUT/'PIM_VIII_TaskFlow.docx'
    with ZipFile(path,'w',ZIP_DEFLATED) as z:
        for n,s in {'[Content_Types].xml':types,'_rels/.rels':rels,'word/_rels/document.xml.rels':docrels,'word/document.xml':doc,'word/styles.xml':styles,'word/settings.xml':settings}.items(): z.writestr(n,s)
    return path

def html_page(num, content, visible=True):
    label=f'<div class="pnum">{num}</div>' if visible else ''
    return f'<section class="page">{label}{content}</section>'

def render_html():
    def heading(s,level=1): return f'<h{level}>{escape(s)}</h{level}>'
    def para_html(s): return ''.join(f'<p>{escape(x)}</p>' for x in s.split('\n\n'))
    pages=[]
    cover=f'<div class="cover"><strong>UNIVERSIDADE PAULISTA – UNIP EaD<br>PROJETO INTEGRADO MULTIDISCIPLINAR VIII</strong><div class="author">{AUTHOR.upper()} – RA {RA}</div><div class="title">TASKFLOW: SISTEMA INTEGRADO PARA GESTÃO DE TAREFAS DA ALUNOS TECH</div><div class="city">São Paulo<br>2026</div></div>'
    pages.append(html_page(1,cover,False))
    title=f'<div class="cover"><strong>{AUTHOR.upper()} – RA {RA}</strong><div class="title">TASKFLOW: SISTEMA INTEGRADO PARA GESTÃO DE TAREFAS DA ALUNOS TECH</div><div class="nature">Projeto Integrado Multidisciplinar apresentado à Universidade Paulista – UNIP EaD, como requisito acadêmico do curso Superior de Tecnologia em Análise e Desenvolvimento de Sistemas, no âmbito do Projeto Integrado Multidisciplinar VIII.</div><div class="city">São Paulo<br>2026</div></div>'
    pages.append(html_page(2,title,False))
    summary = SUMMARY
    pages.append(html_page(3,heading('RESUMO')+f'<div class="abstract">{escape(summary)}</div><p class="kw">Palavras-chave: gestão de tarefas; API; SQL Server; .NET; DevOps.</p>',False))
    pages.append(html_page(4,heading('ABSTRACT')+f'<div class="abstract">{escape(ABSTRACT)}</div><p class="kw">Keywords: task management; API; SQL Server; .NET; DevOps.</p>',False))
    pages.append(html_page(5,heading('LISTA DE ILUSTRAÇÕES')+'<p>Figura 1 – Arquitetura lógica do TaskFlow .................................................. 9</p>',False))
    toc=''.join(f'<div class="toc"><span>{escape(label)}</span><b>{n}</b></div>' for label,n in [("1 INTRODUÇÃO",7),("2 ANÁLISE E ARQUITETURA DA SOLUÇÃO",8),("2.1 Contexto organizacional",8),("2.2 Arquitetura em camadas e fluxo de dados",9),("3 DESENVOLVIMENTO DAS APLICAÇÕES",10),("3.1 API, autenticação e autorização",10),("3.2 Interface Web ASP.NET Core MVC",11),("3.3 Cliente Mobile Flutter",12),("3.4 Cliente Desktop WinForms",13),("4 PROGRAMAÇÃO DE BANCO DE DADOS",14),("4.1 Modelo relacional e integridade",14),("4.2 Função, procedure, trigger e transações",15),("4.3 Consultas, desempenho e concorrência",16),("5 CLOUD COMPUTING E DEVOPS",17),("5.1 Topologia proposta e conteinerização",17),("5.2 CI, Kubernetes e escalabilidade",18),("6 GERENCIAMENTO DE PROJETOS ÁGIL",19),("6.1 Backlog e critérios de aceite",19),("6.2 Sprints, acompanhamento e retrospectiva",20),("7 TESTES E RESULTADOS",21),("7.1 Evidências de validação",21),("7.2 Síntese dos resultados e integração",22),("8 CONCLUSÃO",23),("REFERÊNCIAS",24)])
    pages.append(html_page(6,heading('SUMÁRIO')+toc,False))
    pages.append(html_page(7,heading('1 INTRODUÇÃO')+para_html('\n\n'.join(INTRO))))
    for i,(ch,sub,txt) in enumerate(PAGES):
        content=heading(ch)+heading(sub,2)+para_html(fit_text(txt,1700 if i==1 else 2150))
        if i==1: content+='<div class="figure"><div>Clientes Web · Mobile · Desktop</div><div>↓ HTTP / JWT</div><div>API ASP.NET Core</div><div>↓</div><div>Application / Domain → Infrastructure / EF Core</div><div>↓</div><div>SQL Server</div></div><div class="caption">Figura 1 – Arquitetura lógica do TaskFlow</div><div class="source">Fonte: elaborado pelo autor com base no código do projeto (2026).</div>'
        pages.append(html_page(8+i,content))
    conclusion = CONCLUSION
    pages.append(html_page(23,heading('8 CONCLUSÃO')+''.join(f'<p>{escape(x)}</p>' for x in conclusion)))
    pages.append(html_page(24,heading('REFERÊNCIAS')+''.join(f'<p class="ref">{escape(x)}</p>' for x in REFS)))
    css='''@page{size:A4;margin:3cm 2cm 2cm 3cm}*{box-sizing:border-box}body{font-family:Arial,sans-serif;font-size:12pt;line-height:1.5;color:#111;margin:0}.page{min-height:24.7cm;page-break-after:always;position:relative}.page:last-child{page-break-after:auto}.pnum{position:absolute;top:0;right:0;font-size:12pt}h1{text-align:left;font-size:12pt;margin:0 0 12pt;font-weight:bold}h2{font-size:12pt;margin:0 0 10pt;font-weight:bold}p{text-align:justify;text-indent:1.5cm;margin:0 0 8pt}.abstract{line-height:1.0;text-align:justify}.abstract p{text-indent:0}.kw{margin-top:10pt;text-indent:0}.cover{text-align:center;height:24.7cm;position:relative;font-size:12pt}.author{position:absolute;top:23%;width:100%;font-weight:bold}.title{position:absolute;top:40%;width:100%;font-weight:bold}.city{position:absolute;bottom:0;width:100%}.nature{position:absolute;top:48%;left:45%;width:55%;text-align:justify;line-height:1.0}.toc{display:flex;gap:5pt;align-items:baseline;margin:3pt 0;font-size:9.5pt}.toc span{flex:1;border-bottom:1px dotted #555}.toc b{font-weight:normal}.figure{margin:12pt auto 4pt;width:80%;text-align:center;line-height:1.3}.figure div{padding:4pt;border:1px solid #444}.caption,.source{text-align:center;font-size:10pt;margin:3pt 0}.ref{text-align:left;text-indent:0;line-height:1.0;margin:0 0 12pt;overflow-wrap:anywhere}'''
    html='<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><style>'+css+'</style></head><body>'+''.join(pages)+'</body></html>'
    tmp=Path(tempfile.gettempdir())/'PIM_VIII_TaskFlow_render.html'; tmp.write_text(html,encoding='utf-8'); return tmp

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    docx=write_docx(); html=render_html(); shutil.copyfile(html, OUT/'PIM_VIII_TaskFlow.html')
    candidates=[Path(r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'),Path(r'C:\Program Files\Google\Chrome\Application\chrome.exe')]
    browser=next((p for p in candidates if p.exists()),None)
    if not browser: raise SystemExit('Chrome/Edge não encontrado; PDF não gerado.')
    pdf=OUT/'PIM_VIII_TaskFlow.pdf'
    profile=Path(tempfile.mkdtemp(prefix='taskflow_pdf_'))
    try:
        subprocess.run([str(browser),'--headless','--disable-gpu','--no-sandbox','--no-pdf-header-footer',f'--user-data-dir={profile}',f'--print-to-pdf={pdf}',html.resolve().as_uri()],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    finally:
        shutil.rmtree(profile,ignore_errors=True)
        html.unlink(missing_ok=True)
    print('DOCX:',docx,'PDF:',pdf)

if __name__=='__main__': main()
