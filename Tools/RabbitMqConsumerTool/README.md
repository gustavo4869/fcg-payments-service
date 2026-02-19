# RabbitMQ Consumer Tool

## ?? Objetivo

Ferramenta para visualizar mensagens publicadas no RabbitMQ pela API de Payments.

## ?? Como Usar

### 1. Iniciar RabbitMQ

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 2. Habilitar Mensageria na API

Editar `Fcg.Payments.Api/appsettings.Development.json`:

```json
{
  "Messaging": {
    "Enabled": true
  }
}
```

### 3. Executar o Consumer

```bash
cd Tools/RabbitMqConsumerTool
dotnet run
```

### 4. Criar um Pagamento

```bash
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "gameId": "22222222-2222-2222-2222-222222222222",
    "amount": 99.99
  }'
```

### 5. Ver a Mensagem no Console

```
?? Payment Consumer STARTED - Listening for messages...
??????????????????????????????????????????
?? NEW MESSAGE RECEIVED
??????????????????????????????????????????
?? PAYLOAD:
{
  "paymentId": "...",
  "orderId": null,
  "userId": "11111111-1111-1111-1111-111111111111",
  "gameId": "22222222-2222-2222-2222-222222222222",
  "status": "Succeeded",
  "amount": 99.99,
  "currency": "BRL",
  "processedAt": "2024-02-19T15:30:00Z",
  "correlationId": null
}
???  PROPERTIES:
   MessageId: abc123
   CorrelationId: null
   Timestamp: 2024-02-19 15:30:00
   ContentType: application/json
   Routing Key: payment.processed
   Exchange: payments
??????????????????????????????????????????
?? Payment ... | User ... | Status Succeeded | Amount 99.99 BRL
```

## ??? Configuração

Editar `appsettings.json` para conectar a diferentes ambientes:

```json
{
  "Messaging": {
    "Host": "production-rabbitmq.example.com",
    "Username": "production-user",
    "Password": "production-password"
  }
}
```

## ?? Troubleshooting

**Erro de conexão?**
- Verifique se RabbitMQ está rodando: `docker ps`
- Verifique as credenciais no `appsettings.json`
- Verifique se a API está com `Messaging:Enabled=true`

**Não recebe mensagens?**
- Verifique se a API está publicando (ver logs da API)
- Verifique se a exchange/queue/routing key estão corretos
- Use o RabbitMQ Management UI para verificar bindings
