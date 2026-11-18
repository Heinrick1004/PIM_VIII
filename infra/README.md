# Operação de infraestrutura TaskFlow

## Estado da implementação

- Dockerfiles multi-stage separados para API e MVC, .NET 10, processo `app` não privilegiado e health check de container/API. `compose.yaml` é somente para desenvolvimento, não provisiona SQL Server e exige configuração externa.
- `.github/workflows/ci.yml` define CI para Windows (restore, build e testes .NET seguros) e Linux (build das duas imagens). Não faz publicação nem deploy.
- `k8s/` contém Namespace, ConfigMap, Services internos, Ingress TLS, PVC compartilhada do Data Protection, Deployments com probes/recursos/security context e HPAs. É um template: nomes DNS, classe Ingress, storage RWX, métricas e imagens precisam ser adaptados ao cluster.
- O API `/health` é liveness e não verifica dependências. `/ready` testa `CanConnectAsync` no SQL Server, sem revelar detalhes da conexão, e retorna 503 quando o banco não está pronto.
- A configuração local de desenvolvimento expõe portas somente em loopback. Não use `ASPNETCORE_ENVIRONMENT=Development` em produção.

## Docker local

Requisitos: Docker Engine/Desktop com Compose v2, SDK/runtime não necessários para build (Dockerfile usa .NET 10) e um SQL Server de desenvolvimento acessível a partir do container. O banco deve existir e estar migrado; a aplicação não aplica migrations no startup.

~~~powershell
Copy-Item infra/docker/.env.example infra/docker/.env
# Preencha infra/docker/.env localmente (não versione).
docker compose --env-file infra/docker/.env -f infra/docker/compose.yaml config
docker compose --env-file infra/docker/.env -f infra/docker/compose.yaml up --build
~~~

No Windows/macOS, `localhost` dentro do container não aponta para o host; use `host.docker.internal` na connection string quando o SQL estiver no host. A API usa `http://localhost:5046/health` e `/ready`; MVC usa `http://localhost:5080`. O Compose usa ambiente Development para facilitar API→MVC por rede interna e não representa topologia pública segura.

Builds de imagem manuais:

~~~powershell
docker build -f infra/docker/Dockerfile.api -t taskflow-api:local .
docker build -f infra/docker/Dockerfile.web -t taskflow-web:local .
~~~

## Kubernetes

As imagens devem ser construídas e publicadas em registry autorizado pelo operador, e os campos `image` em `k8s/deployments.yaml` devem apontar para tags imutáveis desse registry. Não use `latest` na operação real.

Antes de aplicar os manifests, provisionar no namespace `taskflow`:

- Secret `taskflow-runtime` com as chaves `connection-string`, `jwt-issuer`, `jwt-audience` e `jwt-signing-key`, criado por integração de secrets/CSI ou procedimento protegido do provedor. Não comitar Secret com valores reais.
- Secret TLS `taskflow-ingress-tls` para os domínios configurados no Ingress.
- StorageClass que suporte PVC `ReadWriteMany`, encrypted-at-rest, para as chaves Data Protection do MVC. A API precisa de URL HTTPS externa configurada em `Api__BaseUrl`; isso mantém TLS entre MVC e endpoint API.
- Ingress NGINX ou adaptar `ingressClassName`/anotações, DNS/certificado, metrics-server para HPA e políticas de rede do cluster.

Depois de configurar contexto, images, hosts e dependências externas:

~~~powershell
kubectl apply -f infra/k8s/namespace.yaml
# Criar secrets e configurar storage/DNS/TLS via mecanismos do provedor, fora do repositório.
kubectl apply -f infra/k8s/config.yaml
kubectl apply -f infra/k8s/storage.yaml
kubectl apply -f infra/k8s/deployments.yaml
kubectl rollout status deployment/taskflow-api -n taskflow
kubectl rollout status deployment/taskflow-web -n taskflow
~~~

O MVC persiste e compartilha o key ring em volume entre réplicas. A criptografia at-rest do PVC e o controle de acesso precisam ser garantidos pelo provedor. Os HPAs escalam entre 2 e 6 réplicas por CPU; exigem metrics-server e capacidade de cluster. SQL Server gerenciado deve manter backup/PITR, redundância, firewall privado e plano testado de recuperação; configurar estes itens no provedor antes de produção.

## Arquitetura cloud proposta

Publicar o MVC e a API como serviços .NET separados em Kubernetes gerenciado, atrás de Ingress HTTPS com certificado gerenciado/WAF. Usar SQL Server gerenciado compatível com a aplicação, em rede privada; armazenar connection string e chave JWT em secret manager integrado ao cluster. Centralizar logs estruturados em stdout e encaminhá-los ao serviço de observabilidade do provedor; alertar sobre indisponibilidade, HTTP 5xx, latência, saturação e falha de readiness. Usar `/health` para reinício do container, `/ready` para remover pods quando SQL estiver indisponível e backup/PITR para recuperação. HPA atende variação de carga, sujeito a métricas e capacidade do cluster.

Esta topologia é **proposta**, não implantada. Não há conta/assinatura cloud, registry, domínio, cluster ou credenciais confirmados. Nenhum recurso pago foi criado.

## CI

O GitHub Actions foi preparado em `.github/workflows/ci.yml`, sem secrets porque o fluxo não publica artefatos. O job .NET roda no Windows por causa do cliente WinForms; o job Ubuntu constrói API/MVC Docker. A execução remota só será comprovada após o workflow ser enviado ao GitHub por um responsável autorizado. O pipeline não executa testes SQL cuja fixture cria/remove bancos, nem reconstrói Flutter, já validado e não alterado nesta tarefa.
