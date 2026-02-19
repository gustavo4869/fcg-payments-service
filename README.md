FIAP Cloud Games - Serviço de Pagamentos
======================================

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Azure Container Apps](https://img.shields.io/badge/Azure-Container%20Apps-blue)](https://azure.microsoft.com/services/container-apps/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Deploy Status](https://github.com/gustavo4869/fcg-payments-service/actions/workflows/deploy-payments.yml/badge.svg)](https://github.com/gustavo4869/fcg-payments-service/actions)

> **MVP – Microsserviço de Pagamentos (simulado, orientado a eventos)**

API RESTful desenvolvida em **.NET 10** para criação e processamento assíncrono de pagamentos. Simula processamento com um worker (HostedService) e um Azure Function (timer) e persiste eventos em um Event Store (append-only).

---

## 📋 Sumário

- [Visão Geral](#-visão-geral)
- [Arquitetura](#-arquitetura)
- [Tecnologias](#-tecnologias)
- [Funcionalidades](#-funcionalidades)
- [Estrutura do Projeto](#-estrutura-do-projeto)
- [Fluxo de Comunicação](#-fluxo-de-comunicação)
- [Pré-requisitos](#-pré-requisitos)
- [Instalação e Execução](#-instalação-e-execução)
- [Endpoints da API](#-endpoints-da-api)
- [Docker](#-docker)
- [Health Checks](#-health-checks)
- [Event Sourcing](#-event-sourcing)
- [Variáveis de Ambiente](#-variáveis-de-ambiente)
- [Validações](#-validações)
- [Segurança](#-segurança)

---

## 🎯 Visão Geral

O **FCG Payments Service** é um microsserviço responsável por receber pedidos de pagamento, persistir o estado inicial e publicar eventos no Event Store. Um processador assíncrono (HostedService ou Function) consome pagamentos pendentes, simula o resultado e publica eventos de sucesso/falha.

---

## 🏗️ Arquitetura

### Diagrama de Arquitetura do Sistema

```mermaid
flowchart LR
  Client[Client]
  API["Fcg.Payments.Api<br/>(Minimal API)"]
  Repo["PagamentoRepository<br/>(EF Core) &rarr; Pagamentos table"]
  EventStore["EfEventStore<br/>(Events table)"]
  DB["Shared DB (SQLite)"]
  Functions["Fcg.Payments.Functions<br/>(Timer Trigger)"]
  Consumers["Other consumers<br/>(projections, analytics)"]

  Client -->|HTTP POST /payments| API
  API --> Repo
  API --> EventStore
  Repo --> DB
  EventStore --> DB
  Functions -->|polls pending| Repo
  Functions --> EventStore
  Consumers -->|reads events| EventStore

  subgraph Backend
    API
    Functions
    Consumers
  end

  DB -.-> Backend
```

### Diagrama de Sequência (Fluxo de pagamento)

```mermaid
sequenceDiagram
  participant C as Client
  participant A as API
  participant R as Repositorio\n(DB)
  participant E as EventStore
  participant P as Processador\n(Hosted/Function)

  C->>A: POST /payments (body + X-Correlation-ID?)
  A->>R: INSERT Pagamento (Status=Requested)
  A->>E: APPEND PaymentRequested (payload, correlationId)
  Note right of E: Evento persistido

  P->>R: SELECT Pagamentos WHERE Status=Requested
  P->>P: Simula processamento (70% sucesso)
  P->>R: UPDATE Pagamento (Status Succeeded/Failed)
  P->>E: APPEND PaymentSucceeded/PaymentFailed (payload, correlationId)
```

---

## 🛠️ Tecnologias

| Categoria | Tecnologia | Versão |
|-----------|-----------|--------|
| **Framework** | .NET | 10.0 |
| **API** | ASP.NET Core Minimal APIs | 10.0 |
| **Database** | SQLite | - |
| **ORM** | Entity Framework Core | 10.0 |
| **Validação** | FluentValidation | 11.x |
| **Functions** | Azure Functions (dotnet-isolated) | 4 |
| **Container** | Docker | - |
| **Logging** | Microsoft.Extensions.Logging | - |

---

## ⚡ Funcionalidades

- ➕ Criar pagamento (`PaymentRequested`) com validação básica
- 🔎 Consultar pagamento por id e listar por usuário
- ♻️ Reprocessamento de pagamentos com status `Failed` (cria novo pagamento)
- ⚙️ Processamento assíncrono (HostedService + Azure Function timer)
- 📝 Event Store append-only para auditoria
- 🔁 Correlation ID opcional via header `X-Correlation-ID`

---

## 📁 Estrutura do Projeto (exemplo)

```
Fcg.Payments.Api/
├── Api/
│   └── Endpoints/
│       ├── PagamentosEndpoints.cs
│       └── EventsEndpoints.cs
├── Application/
│   └── Pagamentos/
│       ├── Request.cs
│       └── Response.cs
├── Domain/
│   ├── Entidades/
│   │   └── Pagamento.cs
│   └── Enum/
│       └── PagamentoStatusEnum.cs
├── Infra/
│   ├── Events/
│   │   ├── EventEntity.cs
│   │   ├── IEventStore.cs
│   │   └── EfEventStore.cs
│   ├── Repositorio/
│   │   └── PagamentoRepository.cs
│   └── PagamentoDbContext.cs
├── Setup/
│   ├── ServiceCollectionExtensions.cs
│   └── WebApplicationExtensions.cs
├── Program.cs
├── Dockerfile
└── Migrations/
```

---

## 🔄 Fluxo de Comunicação

- Cliente -> API: cria pagamento (POST /payments).
- API persiste `Pagamento` (Status = Requested) e grava `PaymentRequested` no Event Store.
- Processador (HostedService ou Function) consulta pendentes, processa (simulação) e grava `PaymentSucceeded` ou `PaymentFailed` no Event Store.
- Consumidores externos podem ler tabela `Events` para projeções/integrações.

---

## 📋 Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Opcional) [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) para rodar `Fcg.Payments.Functions` localmente
- Docker (opcional)

**Nota**: Para executar Azure Functions localmente, você NÃO precisa do Azurite/Azure Storage Emulator para desenvolvimento básico. O `local.settings.json` já está configurado para funcionar sem storage local.

---

## 🚀 Instalação e Execução

### Executar API localmente

1. Abrir terminal na pasta do projeto e executar:

```sh
cd Fcg.Payments.Api
dotnet run
```

A API estará disponível na porta configurada (ex.: 8080).

### Executar Functions localmente (opcional)

```sh
cd Fcg.Payments.Functions
func start
```

**Troubleshooting**: Se você receber erro de conexão ao storage (porta 10000):
- ✅ O `local.settings.json` já está configurado para funcionar sem Azurite
- ⚠️ Se o erro persistir, verifique se a configuração está: `"AzureWebJobsStorage": ""`
- 💡 Em produção, o Azure configura automaticamente a connection string real

> Observação: `Fcg.Payments.Functions` aplica migrations automaticamente na inicialização.

---

## 📡 Endpoints da API

Base URL (ex.): `https://localhost:8080`

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

## 🐳 Docker

### API

```sh
docker build -f Fcg.Payments.Api/Dockerfile -t fcg-payments-api:local .
docker run -p 8080:8080 fcg-payments-api:local
```

### Functions (container)

```sh
docker build -f Fcg.Payments.Functions/Dockerfile -t fcg-payments-functions:local .
docker run -p 80:80 fcg-payments-functions:local
```

---

## 🏥 Health Checks

- `/health` — liveness
- `/health/ready` — readiness (checa disponibilidade do banco)

---

## 📊 Event Sourcing

Entidade de evento usada pelo Event Store:

```csharp
public sealed class EventEntity
{
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public int Version { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Payload { get; set; }
}
```

Tipos de eventos presentes no fluxo:

- `PaymentRequested`
- `PaymentSucceeded`
- `PaymentFailed`

---

## ⚙️ Variáveis de Ambiente

### Variáveis Base

| Variável | Descrição | Padrão | Obrigatório |
|----------|-----------|--------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Development` | Não |
| `ConnectionStrings__DefaultConnection` | String de conexão SQLite | `Data Source=fcg.db` | Não |

### Variáveis de Mensageria (RabbitMQ)

O serviço suporta publicação de eventos via RabbitMQ. Para habilitar esta funcionalidade, configure as seguintes variáveis:

| Variável | Descrição | Padrão | Obrigatório | Exemplo |
|----------|-----------|--------|-------------|---------|
| `MESSAGING__ENABLED` | Feature flag para habilitar mensageria | `false` | **Sim** | `true` |
| `MESSAGING__HOST` | Host do RabbitMQ | `localhost` | Sim (se enabled) | `rabbitmq.svc.cluster.local` |
| `MESSAGING__PORT` | Porta do RabbitMQ | `5672` | Não | `5672` |
| `MESSAGING__USERNAME` | Usuário de autenticação | `guest` | Sim (se enabled) | `payments-publisher` |
| `MESSAGING__PASSWORD` | Senha de autenticação | `guest` | Sim (se enabled) | `<secret>` |
| `MESSAGING__VHOST` | Virtual host do RabbitMQ | `/` | Não | `/payments` |
| `MESSAGING__EXCHANGE` | Nome do exchange (topic) | `payments` | Não | `payments` |
| `MESSAGING__ROUTINGKEY` | Routing key para mensagens | `payment.processed` | Não | `payment.processed` |
| `MESSAGING__QUEUE` | Nome da fila (referência) | `payments-processed` | Não | `payments-processed` |

#### Comportamento do Feature Flag

- **`MESSAGING__ENABLED=false`** (padrão): Nenhuma conexão RabbitMQ é estabelecida. O publisher usa implementação `NoOpPaymentEventPublisher` que apenas loga e não publica eventos.
- **`MESSAGING__ENABLED=true`**: Conexão RabbitMQ é estabelecida na inicialização. Eventos são publicados após processamento bem-sucedido dos pagamentos.

#### Formato da Mensagem Publicada

Quando habilitado, o serviço publica mensagens JSON no exchange configurado com o seguinte formato:

```json
{
  "paymentId": "guid",
  "orderId": "guid|null",
  "userId": "guid",
  "gameId": "guid",
  "status": "Succeeded|Failed",
  "amount": 99.99,
  "currency": "BRL",
  "processedAt": "2024-01-15T10:30:00Z",
  "correlationId": "guid|null"
}
```

#### Ponto de Publicação

Os eventos são publicados **após** a transação de atualização do pagamento (`UpdateAsync`) no `PaymentProcessorHostedService`, garantindo que:
1. O estado do pagamento é persistido primeiro
2. O evento só é publicado para pagamentos persistidos com sucesso
3. Falhas na publicação **não afetam** o processamento do pagamento (logged but not thrown)

#### Resiliência

- **Retry automático**: 3 tentativas com backoff exponencial (100ms, 200ms, 300ms)
- **Publisher Confirms**: Ativado para garantir que mensagens foram recebidas pelo broker
- **Persistência**: Mensagens marcadas como persistentes (`BasicProperties.Persistent = true`)
- **Reconnection automática**: Conexão configurada com `AutomaticRecoveryEnabled = true`

#### Exemplo de Configuração no Kubernetes

```yaml
env:
  - name: MESSAGING__ENABLED
    value: "true"
  - name: MESSAGING__HOST
    valueFrom:
      configMapKeyRef:
        name: rabbitmq-config
        key: host
  - name: MESSAGING__PORT
    value: "5672"
  - name: MESSAGING__USERNAME
    valueFrom:
      secretKeyRef:
        name: rabbitmq-credentials
        key: username
  - name: MESSAGING__PASSWORD
    valueFrom:
      secretKeyRef:
        name: rabbitmq-credentials
        key: password
  - name: MESSAGING__VHOST
    value: "/payments"
  - name: MESSAGING__EXCHANGE
    value: "payments"
  - name: MESSAGING__ROUTINGKEY
    value: "payment.processed"
```

Exemplo `appsettings.json` (desenvolvimento local):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=fcg.db"
  },
  "Messaging": {
    "Enabled": false,
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VHost": "/",
    "Exchange": "payments",
    "RoutingKey": "payment.processed",
    "Queue": "payments-processed"
  }
}
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

## 📄 Licença

Projeto de exemplo — adaptar conforme necessidade.

---

## 📞 Contato

**FIAP Cloud Games Team**

- Repositório: https://github.com/gustavo4869/fcg-payments-service

---

**Desenvolvido com ❤️ usando .NET 10**
