FCG Cloud Games - Serviço de Pagamentos
======================================

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Google Cloud Platform](https://img.shields.io/badge/GCP-Kubernetes%20Engine-blue)](https://cloud.google.com/kubernetes-engine)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Event%20Driven-orange)](https://www.rabbitmq.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Deploy Status](https://github.com/gustavo4869/fcg-payments-service/actions/workflows/deploy-payments.yml/badge.svg)](https://github.com/gustavo4869/fcg-payments-service/actions)

> **Fase 4 – Microsserviço de Pagamentos com Processamento Assíncrono Event-Driven**

Sistema completo de pagamentos desenvolvido em **.NET 10** com arquitetura de microsserviços e processamento assíncrono baseado em eventos via RabbitMQ. Inclui API RESTful para criação de pagamentos, Worker Service para processamento assíncrono, Event Store append-only e deploy produtivo no Google Kubernetes Engine (GKE).

---

## 📋 Sumário

- [Visão Geral](#-visão-geral)
- [Arquitetura](#-arquitetura)
  - [Diagrama Completo do Sistema](#diagrama-completo-do-sistema)
  - [Fluxo de Processamento Assíncrono](#fluxo-de-processamento-assíncrono)
- [Tecnologias](#-tecnologias)
- [Funcionalidades](#-funcionalidades)
- [Estrutura do Projeto](#-estrutura-do-projeto)
- [Processamento Assíncrono](#-processamento-assíncrono)
  - [Migração de Azure Function para Worker Service](#migração-de-azure-function-para-worker-service)
  - [Mecanismo de Resiliência](#mecanismo-de-resiliência)
- [Pré-requisitos](#-pré-requisitos)
- [Instalação e Execução](#-instalação-e-execução)
  - [Execução Local](#execução-local)
  - [Docker Compose](#docker-compose)
- [Deploy Cloud (GCP)](#-deploy-cloud-gcp)
  - [Estrutura Kubernetes](#estrutura-kubernetes)
  - [Build e Push para Artifact Registry](#build-e-push-para-artifact-registry)
  - [Deploy no GKE](#deploy-no-gke)
- [Endpoints da API](#-endpoints-da-api)
- [Health Checks](#-health-checks)
- [Event Sourcing](#-event-sourcing)
- [Variáveis de Ambiente](#-variáveis-de-ambiente)
- [Validações](#-validações)
- [Segurança](#-segurança)

---

## 🎯 Visão Geral

O **FCG Payments Service** é um microsserviço completo para processamento de pagamentos em uma plataforma de jogos cloud. Faz parte de uma arquitetura distribuída composta por:

- **Users API**: Gerenciamento de usuários
- **Games API**: Catálogo de jogos
- **Payments API**: Criação e consulta de pagamentos
- **API Gateway**: Roteamento e autenticação centralizada
- **Payments Worker**: Processamento assíncrono de pagamentos via RabbitMQ

O serviço implementa um padrão **Event-Driven Architecture** com:
- ✅ Criação síncrona de pagamentos via API REST
- ✅ Publicação em fila RabbitMQ (`payment.pending`)
- ✅ Processamento assíncrono via Worker Service dedicado
- ✅ Event Store append-only para auditoria e rastreabilidade
- ✅ Idempotência garantida por chave única
- ✅ Deploy produtivo no Google Kubernetes Engine (GKE)

---

## 🏗️ Arquitetura

### Diagrama Completo do Sistema

```mermaid
flowchart TB
    Client[Cliente/Aplicação]
    Gateway[API Gateway]
    
    subgraph Microsserviços
        UsersAPI[Users API]
        GamesAPI[Games API]
        PaymentsAPI[Payments API]
    end
    
    subgraph Processamento Assíncrono
        RabbitMQ[RabbitMQ<br/>payment.pending]
        Worker[Payments Worker<br/>BackgroundService]
    end
    
    subgraph Persistência
        Database[(PostgreSQL<br/>Cloud SQL)]
        EventStore[(Event Store<br/>Append-Only)]
    end
    
    Client --> Gateway
    Gateway --> UsersAPI
    Gateway --> GamesAPI
    Gateway --> PaymentsAPI
    
    PaymentsAPI -->|1. Persiste<br/>Status=Requested| Database
    PaymentsAPI -->|2. Publica<br/>PaymentRequested| EventStore
    PaymentsAPI -->|3. Enfileira<br/>payment.pending| RabbitMQ
    
    RabbitMQ -->|4. Consome<br/>Manual ACK| Worker
    Worker -->|5. Verifica<br/>Idempotência| EventStore
    Worker -->|6. Processa<br/>Simulação| Worker
    Worker -->|7. Atualiza<br/>Status| Database
    Worker -->|8. Grava<br/>PaymentSucceeded/Failed| EventStore
    
    style Worker fill:#4CAF50,stroke:#2E7D32,color:#fff
    style RabbitMQ fill:#FF6F00,stroke:#E65100,color:#fff
    style PaymentsAPI fill:#2196F3,stroke:#1565C0,color:#fff
```

### Fluxo de Processamento Assíncrono

```mermaid
sequenceDiagram
    participant Client
    participant Gateway
    participant PaymentsAPI
    participant Database
    participant EventStore
    participant RabbitMQ
    participant Worker

    Client->>Gateway: POST /payments
    Gateway->>PaymentsAPI: Forward Request
    
    rect rgb(200, 220, 240)
        Note over PaymentsAPI,EventStore: Fase Síncrona (API)
        PaymentsAPI->>Database: INSERT Pagamento<br/>(Status=Requested)
        PaymentsAPI->>EventStore: APPEND PaymentRequested
        PaymentsAPI->>RabbitMQ: PUBLISH payment.pending
    end
    
    PaymentsAPI-->>Client: 201 Created
    
    rect rgb(200, 240, 200)
        Note over RabbitMQ,Worker: Fase Assíncrona (Worker)
        RabbitMQ->>Worker: CONSUME message
        Worker->>EventStore: CHECK idempotency key
        alt Já Processado
            Worker->>RabbitMQ: ACK (skip duplicate)
        else Novo Pagamento
            Worker->>Worker: Simular processamento<br/>(70% sucesso)
            Worker->>Database: UPDATE Status<br/>(Succeeded/Failed)
            Worker->>EventStore: APPEND PaymentSucceeded<br/>ou PaymentFailed
            Worker->>RabbitMQ: ACK (success)
        end
    end
```

---

## 🛠️ Tecnologias

| Categoria | Tecnologia | Versão | Propósito |
|-----------|-----------|--------|-----------|
| **Framework** | .NET | 10.0 | Runtime e SDK principal |
| **API** | ASP.NET Core Minimal APIs | 10.0 | Endpoints HTTP |
| **Database** | PostgreSQL (Cloud SQL) | 16 | Persistência principal |
| **ORM** | Entity Framework Core | 10.0 | Acesso a dados |
| **Message Broker** | RabbitMQ | 3.13 | Fila de mensagens assíncronas |
| **Worker** | .NET Worker Service | 10.0 | Processamento em background |
| **Validação** | FluentValidation | 11.x | Validação de input |
| **Container** | Docker | latest | Containerização |
| **Orquestração** | Kubernetes (GKE) | 1.28+ | Deploy e escalabilidade |
| **Registry** | Google Artifact Registry | - | Armazenamento de imagens |
| **Logging** | Microsoft.Extensions.Logging | - | Observabilidade |

---

## ⚡ Funcionalidades

### API de Pagamentos
- ➕ **Criar pagamento**: Endpoint `POST /payments` com validação via FluentValidation
- 🔎 **Consultar pagamento**: Busca por ID e listagem por usuário
- ♻️ **Reprocessamento**: Permite reprocessar pagamentos com falha
- 🔐 **Autenticação/Autorização**: Integração com regras de acesso (AdminOnly)
- 📊 **Health Checks**: Monitoramento de banco de dados e RabbitMQ
- 📝 **Event Store**: Auditoria completa com eventos append-only

### Worker de Processamento
- 🔄 **Consumo de fila RabbitMQ**: Processa mensagens da fila `payment.pending`
- 🛡️ **Idempotência**: Evita processamento duplicado via chave única
- ✅ **Manual ACK**: Garante processamento confiável
- 🔁 **Reconexão automática**: Resiliência em caso de falha de conexão
- 📈 **Escalabilidade**: Múltiplas réplicas podem processar em paralelo
- 🎲 **Simulação realista**: 70% de sucesso, 30% de falha (para testes)

---

## 📁 Estrutura do Projeto

```
fcg-payments-service/
├── Fcg.Payments.Api/                    # API RESTful de Pagamentos
│   ├── Api/
│   │   ├── Endpoints/
│   │   │   ├── PagamentosEndpoints.cs   # CRUD de pagamentos
│   │   │   └── EventsEndpoints.cs       # Consulta de eventos
│   │   └── Middleware/
│   │       ├── ErrorMiddleware.cs       # Tratamento de erros global
│   │       └── RequestLoggingMiddleware.cs
│   ├── Application/
│   │   └── Pagamentos/
│   │       ├── Request.cs
│   │       ├── Response.cs
│   │       └── CriarPagamentoValidator.cs
│   ├── Domain/
│   │   ├── Entidades/
│   │   │   └── Pagamento.cs
│   │   ├── Enum/
│   │   │   └── PagamentoStatusEnum.cs
│   │   ├── Messaging/
│   │   │   ├── IPaymentRequestPublisher.cs
│   │   │   └── IPaymentEventPublisher.cs
│   │   └── Repositorio/
│   │       └── IPagamentoRepository.cs
│   ├── Infra/
│   │   ├── Events/
│   │   │   ├── EventEntity.cs
│   │   │   ├── IEventStore.cs
│   │   │   └── EfEventStore.cs
│   │   ├── Messaging/
│   │   │   ├── RabbitMqPaymentRequestPublisher.cs
│   │   │   ├── RabbitMqPaymentEventPublisher.cs
│   │   │   ├── NoOpPaymentRequestPublisher.cs
│   │   │   └── NoOpPaymentEventPublisher.cs
│   │   ├── Repositorio/
│   │   │   └── PagamentoRepository.cs
│   │   └── PagamentoDbContext.cs
│   ├── Setup/
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── WebApplicationExtensions.cs
│   │   └── MessagingOptionsValidator.cs
│   ├── Migrations/
│   ├── Program.cs
│   └── Dockerfile
│
├── Fcg.Payments.Worker/                 # Worker Service (BackgroundService)
│   ├── PaymentQueueWorker.cs           # Consumidor da fila RabbitMQ
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Dockerfile
│   └── README.md
│
├── Fcg.Payments.Functions/              # Azure Function (DESATIVADA)
│   ├── PaymentProcessorFunction.cs     # Timer trigger (não usado no GKE)
│   └── local.settings.json
│
├── k8s/                                 # Manifestos Kubernetes
│   ├── namespace.yaml
│   ├── configmap.yaml
│   ├── secrets.yaml
│   ├── fcg-payments-fixed.yaml         # Deployment API
│   ├── fcg-payments-function-fixed.yaml # Deployment Function (replicas=0)
│   ├── ingress.yaml
│   └── hpa.yaml
│
├── payments-worker.deployment.yaml      # Deployment Worker (ATIVO)
├── README.md                            # Esta documentação
└── FASE_4_DELIVERY.md                   # Documento de entrega formal
```

---

## 🔄 Processamento Assíncrono

### Fluxo Detalhado

#### 1️⃣ **Criação do Pagamento (API)**
```
Cliente → Gateway → Payments API
├─ Valida requisição (FluentValidation)
├─ Cria registro Pagamento (Status = Requested)
├─ Grava evento PaymentRequested no Event Store
└─ Publica mensagem na fila RabbitMQ (payment.pending)
```

#### 2️⃣ **Consumo e Processamento (Worker)**
```
Worker consome mensagem da fila
├─ Verifica idempotência (evita duplicação)
├─ Simula processamento (70% sucesso, 30% falha)
├─ Atualiza status no banco (Succeeded ou Failed)
├─ Grava evento PaymentSucceeded/PaymentFailed
└─ Envia ACK ao RabbitMQ (confirma processamento)
```

#### 3️⃣ **Resiliência e Garantias**
- ✅ **Manual ACK**: Worker só confirma após processamento completo
- ✅ **Requeue em falha**: Erros de processamento devolvem mensagem à fila
- ✅ **Idempotência**: Chave única evita processar o mesmo pagamento 2x
- ✅ **Reconexão automática**: Worker reconecta se perder conexão com RabbitMQ
- ✅ **Prefetch limit**: Controla quantidade de mensagens processadas simultaneamente

### Migração de Azure Function para Worker Service

#### ⚠️ Por que a mudança?

Durante a implantação no **Google Kubernetes Engine (GKE)**, identificamos incompatibilidades críticas com o Azure Functions Host:

**Problemas identificados:**
- ❌ Azure Functions requer **AzureWebJobsStorage** (Azure Storage Account)
- ❌ Não é possível executar Azure Functions nativamente no GKE sem dependências do Azure
- ❌ Complexidade operacional desnecessária para ambiente GCP
- ❌ Polling database é menos eficiente que consumo direto de fila

**Solução implementada:**
- ✅ Migração para **.NET Worker Service** (BackgroundService)
- ✅ Consumo direto da fila RabbitMQ com `RabbitMQ.Client`
- ✅ Deploy nativo como **Deployment Kubernetes**
- ✅ Melhor controle de concorrência e escalabilidade
- ✅ Arquitetura cloud-agnostic (pode rodar em GCP, Azure, AWS, on-premise)

#### 📊 Comparação Técnica

| Critério | Azure Function (Timer) | Worker Service (BackgroundService) |
|----------|------------------------|-----------------------------------|
| **Trigger** | Timer (polling database) | Consumo direto de fila RabbitMQ |
| **Eficiência** | Baixa (consulta periódica) | Alta (event-driven) |
| **Deploy GKE** | Requer emulação Azure | Nativo Kubernetes |
| **Dependências** | Azure Storage Account | Apenas RabbitMQ |
| **Escalabilidade** | Limitada (uma instância) | Horizontal (múltiplas réplicas) |
| **Idempotência** | Manual (via banco) | Garantida (chave única + ACK) |
| **Resiliência** | Limitada | Alta (reconnect + requeue) |
| **Status Atual** | ❌ Desativada (replicas=0) | ✅ **ATIVA EM PRODUÇÃO** |

### Mecanismo de Resiliência

#### Idempotência
```csharp
var idempotencyKey = $"payment-processed:{paymentId}";
var existing = await eventStore.GetByIdempotencyKeyAsync(idempotencyKey);
if (existing != null)
{
    logger.LogInformation("Payment already processed. Skipping.");
    return; // ACK sem reprocessar
}
```

#### Manual ACK com Retry
```csharp
try
{
    await ProcessMessageAsync(message);
    await channel.BasicAckAsync(deliveryTag, false); // ✅ Sucesso
}
catch (JsonException)
{
    await channel.BasicNackAsync(deliveryTag, false, requeue: false); // ❌ JSON inválido
}
catch (Exception)
{
    await channel.BasicNackAsync(deliveryTag, false, requeue: true); // ♻️ Reprocessar
}
```

#### Reconexão Automática
```csharp
var factory = new ConnectionFactory
{
    Uri = new Uri(connectionString),
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
};
```

---

## 📋 Pré-requisitos

### Desenvolvimento Local
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (para RabbitMQ e PostgreSQL locais)
- Editor de código (Visual Studio 2022 17.12+, VS Code ou Rider)

### Deploy em Produção (GCP)
- Conta Google Cloud Platform com faturamento ativado
- [Google Cloud CLI (gcloud)](https://cloud.google.com/sdk/docs/install)
- [kubectl](https://kubernetes.io/docs/tasks/tools/) configurado
- Cluster GKE criado
- Artifact Registry configurado

---

## 🚀 Instalação e Execução

### Execução Local

#### 1. Subir dependências com Docker Compose

Crie um arquivo `docker-compose.yml` na raiz:

```yaml
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: fcg_payments
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: 081160Ec!
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3.13-management
    environment:
      RABBITMQ_DEFAULT_USER: fcg
      RABBITMQ_DEFAULT_PASS: 081160Ec!
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq

volumes:
  postgres_data:
  rabbitmq_data:
```

Execute:
```bash
docker compose up -d
```

#### 2. Executar API

```bash
cd Fcg.Payments.Api
dotnet run
```

A API estará disponível em: `http://localhost:8080` (ou porta configurada)

#### 3. Executar Worker

```bash
cd Fcg.Payments.Worker
dotnet run
```

O Worker começará a consumir a fila `payment.pending` automaticamente.

#### 4. Testar o fluxo completo

```bash
# Criar pagamento
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 12345678-1234-1234-1234-123456789012" \
  -d '{
    "userId": "a1b2c3d4-0000-0000-0000-000000000001",
    "gameId": "e5f6g7h8-0000-0000-0000-000000000002",
    "amount": 59.90
  }'

# Verificar logs do Worker
# O Worker processará automaticamente e atualizará o status
```

### Docker Compose

Execute toda a stack (API + Worker + dependências):

```bash
# Build das imagens
docker build -t fcg-payments-api:local -f Fcg.Payments.Api/Dockerfile .
docker build -t fcg-payments-worker:local -f Fcg.Payments.Worker/Dockerfile .

# Executar com compose
docker compose up
```

> Observação: `Fcg.Payments.Functions` aplica migrations automaticamente na inicialização.

---

## ☁️ Deploy Cloud (GCP)

### Estrutura Kubernetes

O sistema está organizado em componentes Kubernetes:

```
Namespace: fcg
│
├── ConfigMap: payments-config
│   ├── DatabaseProvider = PostgreSQL
│   ├── PaymentQueueName = payment.pending
│   └── ...
│
├── Secret: fcg-secrets
│   ├── ConnectionStrings__DefaultConnection (Cloud SQL)
│   └── RabbitMqConnection
│
├── Deployment: fcg-payments-api
│   ├── Replicas: 2 (escalável via HPA)
│   ├── Image: gcr.io/.../payments-api:latest
│   ├── Service: fcg-payments-svc (ClusterIP)
│   └── Health: /health e /health/ready
│
├── Deployment: fcg-payments-worker (⭐ ATIVO)
│   ├── Replicas: 1 (escalável manualmente)
│   ├── Image: southamerica-east1-docker.pkg.dev/.../payments-worker:latest
│   ├── Consome: payment.pending do RabbitMQ
│   └── Lifecycle: graceful shutdown com preStop hook
│
├── Deployment: fcg-payments-function (❌ DESATIVADA)
│   └── Replicas: 0 (mantida para referência histórica)
│
├── Service: rabbitmq-svc
│   └── RabbitMQ interno (não exposto externamente)
│
└── Ingress: fcg-ingress
    └── Roteamento HTTP externo
```

### Build e Push para Artifact Registry

#### 1. Configurar autenticação

```bash
# Autenticar com GCP
gcloud auth login
gcloud config set project tech-challenge-fase-5-487617

# Configurar Docker para Artifact Registry
gcloud auth configure-docker southamerica-east1-docker.pkg.dev
```

#### 2. Build das imagens

```bash
# API
docker build -t southamerica-east1-docker.pkg.dev/tech-challenge-fase-5-487617/fcg-docker/payments-api:latest \
  -f Fcg.Payments.Api/Dockerfile .

# Worker
docker build -t southamerica-east1-docker.pkg.dev/tech-challenge-fase-5-487617/fcg-docker/payments-worker:latest \
  -f Fcg.Payments.Worker/Dockerfile .
```

#### 3. Push para o registry

```bash
docker push southamerica-east1-docker.pkg.dev/tech-challenge-fase-5-487617/fcg-docker/payments-api:latest
docker push southamerica-east1-docker.pkg.dev/tech-challenge-fase-5-487617/fcg-docker/payments-worker:latest
```

### Deploy no GKE

#### 1. Conectar ao cluster

```bash
gcloud container clusters get-credentials fcg-cluster \
  --region southamerica-east1 \
  --project tech-challenge-fase-5-487617
```

#### 2. Criar namespace e recursos

```bash
# Criar namespace
kubectl apply -f k8s/namespace.yaml

# ConfigMap e Secrets
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
```

#### 3. Deploy dos serviços

```bash
# Deploy API
kubectl apply -f k8s/fcg-payments-fixed.yaml

# Deploy Worker (ATIVO)
kubectl apply -f payments-worker.deployment.yaml

# Desativar Function (se ainda não estiver)
kubectl apply -f k8s/fcg-payments-function-fixed.yaml  # replicas: 0
```

#### 4. Verificar status

```bash
# Ver pods
kubectl get pods -n fcg

# Logs do Worker
kubectl logs -f -n fcg -l app=fcg-payments-worker

# Logs da API
kubectl logs -f -n fcg -l app=fcg-payments-api

# Verificar fila RabbitMQ
kubectl port-forward -n fcg svc/rabbitmq-svc 15672:15672
# Acessar: http://localhost:15672 (fcg / 081160Ec!)
```

#### 5. Escalar Worker (se necessário)

```bash
# Aumentar réplicas do Worker
kubectl scale deployment fcg-payments-worker -n fcg --replicas=3

# Verificar escalabilidade
kubectl get hpa -n fcg  # Se HPA estiver configurado
```

### Estratégia de Deployment

#### Rolling Update
```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 25%        # Pode criar 25% de pods extras durante update
    maxUnavailable: 0    # Garante zero downtime
```

#### Graceful Shutdown
```yaml
terminationGracePeriodSeconds: 60
lifecycle:
  preStop:
    exec:
      command: ["/bin/sh", "-c", "sleep 10"]
```

Isso garante que:
- Worker finaliza processamento de mensagens em andamento
- ACK é enviado ao RabbitMQ antes do pod ser terminado
- Nenhuma mensagem é perdida durante deploy

---

## 📡 Endpoints da API

Base URL (Produção GKE): `http://<INGRESS_IP>/payments`
Base URL (Local): `http://localhost:8080`

- POST `/payments` — cria pagamento
  - Body: `{ "userId": "guid", "gameId": "guid", "amount": decimal }`
  - Header opcional: `X-Correlation-ID: <guid>`
  - Resposta: `201 Created` com `PagamentoResponse`

- GET `/payments/{id}` — obter pagamento
- GET `/payments/by-user/{userId}` — lista do usuário
- POST `/payments/{id}/reprocess` — reprocessar (AdminOnly)
- GET `/events/{aggregateId}` — eventos do aggregate

Exemplo de criação:

```sh
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: <guid>" \
  -d '{"userId":"<guid>","gameId":"<guid>","amount":9.99}'
```

---

## 🏥 Health Checks

A API expõe endpoints de health check para monitoramento:

- **`GET /health`** — Liveness probe (sempre retorna 200)
- **`GET /health/ready`** — Readiness probe (verifica conexão com banco de dados)

Configuração no Kubernetes:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

---

## 📊 Event Sourcing

O sistema implementa **Event Store append-only** para auditoria completa e rastreabilidade.

### Estrutura de Eventos

```csharp
public sealed class EventEntity
{
    public int Id { get; set; }              // Chave primária sequencial
    public Guid AggregateId { get; set; }    // ID do Pagamento
    public string EventType { get; set; }     // PaymentRequested, PaymentSucceeded, PaymentFailed
    public string PayloadJson { get; set; }   // Dados do evento em JSON
    public string? IdempotencyKey { get; set; } // Chave para idempotência
    public DateTime Timestamp { get; set; }   // Data/hora do evento
}
```

### Tipos de Eventos

| Evento | Momento | Publicado Por | Payload |
|--------|---------|---------------|---------|
| **PaymentRequested** | Criação do pagamento | Payments API | `{paymentId, userId, gameId, amount, status, occurredAt}` |
| **PaymentSucceeded** | Processamento bem-sucedido | Payments Worker | `{paymentId, userId, gameId, amount, processedAt, correlationId}` |
| **PaymentFailed** | Processamento com falha | Payments Worker | `{paymentId, userId, gameId, amount, error, processedAt}` |

### Consulta de Eventos

Endpoint: `GET /events`

```bash
# Listar todos os eventos
curl http://localhost:8080/events

# Filtrar por agregado
curl http://localhost:8080/events?aggregateId=<payment-id>

# Filtrar por tipo
curl http://localhost:8080/events?eventType=PaymentSucceeded
```

### Benefícios do Event Sourcing

- 📜 **Auditoria completa**: Histórico imutável de todas as operações
- 🔍 **Debugging**: Rastrear exatamente o que aconteceu com cada pagamento
- 📊 **Analytics**: Consumir eventos para relatórios e métricas
- 🔄 **Replay**: Reconstruir estado a partir dos eventos (se necessário)
- 🧪 **Testes**: Facilita testes de integração e comportamento

---

## ⚙️ Variáveis de Ambiente

### Payments API

| Variável | Descrição | Padrão | Obrigatório |
|----------|-----------|--------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Development` | Não |
| `ConnectionStrings__DefaultConnection` | String de conexão do banco | `Data Source=fcg.db` | Sim |
| `DatabaseProvider` | Provedor do banco | `SQLite` | Não |
| `MESSAGING__ENABLED` | Feature flag para RabbitMQ | `false` | Sim |
| `MESSAGING__HOST` | Host do RabbitMQ | `localhost` | Sim (se enabled) |
| `MESSAGING__PORT` | Porta do RabbitMQ | `5672` | Não |
| `MESSAGING__USERNAME` | Usuário RabbitMQ | `guest` | Sim (se enabled) |
| `MESSAGING__PASSWORD` | Senha RabbitMQ | `guest` | Sim (se enabled) |
| `MESSAGING__VHOST` | Virtual host | `/` | Não |
| `MESSAGING__EXCHANGE` | Exchange para publicar | `payments` | Não |
| `MESSAGING__ROUTINGKEY` | Routing key | `payment.requested` | Não |
| `MESSAGING__QUEUE` | Nome da fila | `payment.pending` | Não |

### Payments Worker

| Variável | Descrição | Padrão | Obrigatório |
|----------|-----------|--------|-------------|
| `ConnectionStrings__DefaultConnection` | String de conexão do banco | - | Sim |
| `DatabaseProvider` | Provedor do banco (`SQLite` ou `PostgreSQL`) | `SQLite` | Não |
| `PaymentQueueName` | Nome da fila a consumir | `payment.pending` | Sim |
| `RabbitMqConnection` | Connection string RabbitMQ | - | Sim |

#### Exemplo - ConnectionString RabbitMQ
```
amqp://username:password@host:port/vhost
amqp://fcg:081160Ec!@rabbitmq-svc:5672/
```

### Exemplo de Configuração Completa

#### Desenvolvimento Local (`appsettings.json`)

**Payments API**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=fcg.db"
  },
  "DatabaseProvider": "SQLite",
  "Messaging": {
    "Enabled": true,
    "Host": "localhost",
    "Port": 5672,
    "Username": "fcg",
    "Password": "081160Ec!",
    "VHost": "/",
    "Exchange": "payments",
    "RoutingKey": "payment.requested",
    "Queue": "payment.pending"
  }
}
```

**Payments Worker**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=fcg.db"
  },
  "DatabaseProvider": "SQLite",
  "PaymentQueueName": "payment.pending",
  "RabbitMqConnection": "amqp://fcg:081160Ec!@localhost:5672/"
}
```

#### Produção GKE (ConfigMap + Secret)

```yaml
# ConfigMap
apiVersion: v1
kind: ConfigMap
metadata:
  name: payments-config
  namespace: fcg
data:
  DatabaseProvider: "PostgreSQL"
  PaymentQueueName: "payment.pending"
  MESSAGING__ENABLED: "true"
  MESSAGING__HOST: "rabbitmq-svc"
  MESSAGING__EXCHANGE: "payments"

---
# Secret
apiVersion: v1
kind: Secret
metadata:
  name: fcg-secrets
  namespace: fcg
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=10.x.x.x;Database=fcg_payments;Username=postgres;Password=..."
  RabbitMqConnection: "amqp://fcg:password@rabbitmq-svc:5672/"
  MESSAGING__PASSWORD: "secure-password"
```

---

## ✅ Validações

- `userId` e `gameId` devem ser GUIDs válidos
- `amount` deve ser > 0

As validações são aplicadas via `FluentValidation`.

---

## 🔒 Segurança

- Exemplos de autorização estão aplicados em endpoints (regras como `AdminOnly` para reprocessamento).
- Em produção, executar via HTTPS e proteger o acesso ao banco.

---

**Desenvolvido com ❤️ usando .NET 10 | Powered by Google Kubernetes Engine**
