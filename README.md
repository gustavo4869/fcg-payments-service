Fcg Payments Service
====================

Resumo
------
Projeto exemplo de um serviço de pagamentos minimalista implementado em .NET 10. Ele contém:

- `Fcg.Payments.Api`: uma API minimal com endpoints para criar/consultar/reprocessar pagamentos e um endpoint para consultar o Event Store.
- `Fcg.Payments.Functions`: um host Azure Functions (timer trigger) que processa pagamentos pendentes (simulação).
- Persistência usando EF Core + SQLite (migrations incluídas).
- Event Store (tabela `Events`) onde todos os eventos do domínio são append-only.

Objetivo: demonstrar um fluxo de pagamentos orientado a eventos com processamento assíncrono e reprocessamento.

Componentes principais
----------------------
- `Pagamento` (entidade): representa um pagamento com campos `Id`, `UserId`, `GameId`, `Amount`, `Status`, `DataCriacao`.
  - Status possíveis: `Requested (1)`, `Succeeded (2)`, `Failed (3)`.
- Repositório: `IPagamentoRepository` / `PagamentoRepository` usando `PagamentoDbContext` (EF Core).
- Event Store: `IEventStore` / `EfEventStore` que grava `EventEntity` (EventId, AggregateId, EventType, OccurredAt, Version, CorrelationId, Payload) na tabela `Events`.
- Processadores de pagamento:
  - `PaymentProcessorHostedService` (background service na API) — verifica pendentes a cada 5s.
  - `PaymentProcessorFunction` (Azure Function timer) — executa a cada 10s.
  - Ambos simulam processamento com 70% de sucesso e publicam eventos `PaymentSucceeded` ou `PaymentFailed` no Event Store.
- Endpoints HTTP (implementados em `PagamentosEndpoints` e `EventsEndpoints`).

Endpoints principais
--------------------
- POST `/payments` (criar pagamento)
  - Corpo: `{ "userId": "guid", "gameId": "guid", "amount": decimal }`
  - Validação: campos obrigatórios + `amount > 0`.
  - Produz evento `PaymentRequested` no Event Store. Requer autorização.
  - Retorna `201 Created` com `PagamentoResponse`.

- GET `/payments/{id}` (consultar pagamento)
  - Retorna `200 OK` com `PagamentoResponse` ou `404`.
  - Permite anonymous.

- GET `/payments/by-user/{userId}`
  - Lista pagamentos do usuário. Requer autorização.

- POST `/payments/{id}/reprocess` (reprocessamento)
  - Apenas para status `Failed`.
  - Cria um novo pagamento (novo aggregate) e publica `PaymentRequested` para reprocessamento.
  - Requer autorização `AdminOnly`.

- GET `/events/{aggregateId}`
  - Retorna lista de eventos para o `aggregateId` (Event Store read).

Fluxo de comunicação entre microsserviços / componentes
-----------------------------------------------------
Fluxo principal (criar -> processar -> evento):

1. Cliente chama POST `/payments` na API.
2. API valida e persiste uma nova entidade `Pagamento` com status `Requested`.
3. API grava um evento `PaymentRequested` no Event Store (tabela `Events`) com payload contendo dados do pagamento.
4. Um processador (ou o `PaymentProcessorHostedService` executando dentro da API, ou `PaymentProcessorFunction` rodando em ambiente Functions) consulta periodicamente pagamentos com status `Requested` do banco.
5. Para cada pagamento pendente, o processador simula o resultado (70% de sucesso) e marca o pagamento como `Succeeded` ou `Failed` no repositório.
6. O processador grava um evento `PaymentSucceeded` ou `PaymentFailed` no Event Store com payload e, opcionalmente, `CorrelationId` se informado pelo cliente na requisição original (header `X-Correlation-ID`).

Observações de comunicação:
- Persistência e Event Store são compartilhados via banco (SQLite por padrão). Não há um barramento externo neste exemplo — o Event Store é a fonte de verdade para eventos.
- Correlação: clientes podem enviar `X-Correlation-ID` em `POST /payments`. Esse `CorrelationId` é armazenado nos eventos subsequentes quando presente.

Arquitetura (diagrama simplificado)
-----------------------------------
ASCII diagram:

 Client
   |
   | HTTP
   v
 Fcg.Payments.Api (Minimal API)
   |- `PagamentoRepository` (EF Core) -> `Pagamentos` table
   |- `EfEventStore` (EF Core) -> `Events` table
   |- `PaymentProcessorHostedService` (opcional, background)
   |
   +-----------------------+
                           |
                           v
                 Shared Database (SQLite)
                   /            \\
                  /              \\
                 v                v
  Fcg.Payments.Functions        Other consumers
  (Timer Trigger)               (e.g. analytics, projections)
  - `PaymentProcessorFunction`  - read events from `Events` table
  - uses same `PagamentoDbContext`

Descrição do diagrama:
- A API é responsável por aceitar comandos (ex.: `PaymentRequested`) e persistir o estado inicial.
- O Event Store fica no mesmo banco e armazena os eventos por aggregate id.
- O processamento assíncrono (hosted service ou function) consome o estado (consultando a tabela `Pagamentos`) e publica novos eventos no Event Store.
- Outros consumidores (não implementados) poderiam ler a tabela `Events` para projeções, integrações ou notificações.

Detalhamento do fluxo (exemplo com correlation id)
-------------------------------------------------
- Requisição cliente com header `X-Correlation-ID: <guid>` ao criar pagamento.
- API grava `PaymentRequested` com `CorrelationId` informado.
- Processador, quando gravar `PaymentSucceeded` ou `PaymentFailed`, copia o mesmo `CorrelationId` para o evento.

Banco de dados e migrations
---------------------------
- Projeto contém migrations (pasta `Migrations`) com `InitialCreate` que cria as tabelas `Pagamentos` e `Events`.
- Connection string padrão: `Data Source=fcg.db` (arquivo SQLite no diretório do app). Pode ser configurada via `ConnectionStrings:DefaultConnection`.
- `Fcg.Payments.Functions` Program.cs tenta resolver caminhos relativos do `Data Source` para tornar o arquivo SQLite acessível ao Functions worker.

Como executar localmente
------------------------
Pré-requisitos:
- .NET 10 SDK
- (opcional) Azure Functions Core Tools para executar as Functions localmente

Executar API (local):
- Entrar na pasta `Fcg.Payments.Api` e rodar:
  - `dotnet run`  (ou publicar e executar a DLL)

Executar Functions (local):
- Entrar na pasta `Fcg.Payments.Functions` e usar Azure Functions Core Tools:
  - `func start`  (requer `func` instalado)
- Alternativamente, publicar/rodar o projeto Functions com `dotnet` em um host que suporte Functions worker.

Como executar com Docker
------------------------
- API:
  - `docker build -f Fcg.Payments.Api/Dockerfile -t fcg-payments-api:local .`
  - `docker run -e "ASPNETCORE_ENVIRONMENT=Development" -p 8080:8080 fcg-payments-api:local`

- Functions (container):
  - `docker build -f Fcg.Payments.Functions/Dockerfile -t fcg-payments-functions:local .`
  - `docker run -p 80:80 fcg-payments-functions:local`

Exemplos de requisições
-----------------------
Criar pagamento:
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: <guid>" \
  -d '{"userId":"<guid>","gameId":"<guid>","amount":9.99}'

Consultar pagamento:
curl http://localhost:8080/payments/<paymentId>

Listar eventos de um aggregate:
curl http://localhost:8080/events/<aggregateId>

Reprocessar (AdminOnly — precisa de autorização configurada):
curl -X POST http://localhost:8080/payments/<failedPaymentId>/reprocess

Observabilidade e logs
----------------------
- O processamento (hosted service e function) grava logs com informações sobre cada pagamento processado e o resultado.
- Em Azure ou contêiner, basta expor e coletar logs padrão do processo.

Notas de arquitetura e limitações
--------------------------------
- Exemplo simples: não existe um barramento de mensagens externo nem publish/subscribe real — o Event Store é uma tabela compartilhada.
- Em produção, recomenda-se usar um banco dedicado com concorrência adequada, ou um broker/event bus (Kafka, RabbitMQ, EventGrid), particionamento, e mecanismos de retry/backoff mais robustos.
- O reprocessamento aqui cria um novo aggregate em vez de reusar o mesmo id para simplificar o exemplo.

Contribuição
------------
- Executar `dotnet ef database update` (ou deixar o app aplicar migrations automaticamente) para preparar o banco.
- Abrir PR com melhorias (ex.: retries, métricas, separação de leitura/escrita para Event Store).

Licença
-------
Projeto de exemplo — adapte conforme necessidade.
