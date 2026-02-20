# Payment Queue Worker

Worker Service que processa pagamentos pendentes de uma fila RabbitMQ.

## Configuração

### Variáveis de Ambiente Necessárias

#### Banco de Dados
- `ConnectionStrings__DefaultConnection`: String de conexão do banco de dados
- `DatabaseProvider`: `SQLite` ou `PostgreSQL` (padrão: `PostgreSQL`)

#### RabbitMQ
- `PaymentQueueName`: Nome da fila a consumir (padrão: `payment.pending`)
- `RabbitMqConnection`: Connection string do RabbitMQ no formato `amqp://username:password@host:port/`

### Arquivo de Configuração Local

Para desenvolvimento local, copie `appsettings.json.example` para `appsettings.json` e configure:

```bash
cp appsettings.json.example appsettings.json
```

Edite o `appsettings.json` com suas credenciais:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fcg_payments;Username=postgres;Password=081160Ec!;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;"
  },
  "DatabaseProvider": "PostgreSQL",
  "PaymentQueueName": "payment.pending",
  "RabbitMqConnection": "amqp://fcg:081160Ec!@localhost:5672/"
}
```

### Conectar ao PostgreSQL via Cloud SQL Proxy

Para conectar ao Cloud SQL do Google Cloud:

1. **Instale o Cloud SQL Proxy**:
```bash
# Windows
curl -o cloud-sql-proxy.exe https://dl.google.com/cloudsql/cloud_sql_proxy_x64.exe

# Linux/Mac
curl -o cloud-sql-proxy https://dl.google.com/cloudsql/cloud_sql_proxy_linux_amd64
chmod +x cloud-sql-proxy
```

2. **Execute o Cloud SQL Proxy**:
```bash
# Windows
.\cloud-sql-proxy.exe YOUR-PROJECT:YOUR-REGION:YOUR-INSTANCE --port=5432

# Linux/Mac
./cloud-sql-proxy YOUR-PROJECT:YOUR-REGION:YOUR-INSTANCE --port=5432
```

3. **Configure a connection string para localhost**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fcg_payments;Username=postgres;Password=081160Ec!;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;"
  },
  "DatabaseProvider": "PostgreSQL"
}
```

### Exemplo para SQLite (desenvolvimento local)

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

## Migrations

As migrations são compartilhadas com o projeto API. Para aplicar:

```bash
# Aplicar migrations automaticamente (já configurado no startup)
dotnet run --project Fcg.Payments.Worker

# Ou manualmente via CLI
dotnet ef database update --project Fcg.Payments.Api --startup-project Fcg.Payments.Worker
```

## Executar

```bash
dotnet run --project Fcg.Payments.Worker
```

## Deploy em Kubernetes

As variáveis devem ser definidas via ConfigMap ou Secrets.
