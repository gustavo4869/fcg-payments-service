using Fcg.Payments.Api.Domain.Messaging;
using System.Text.Json;
using Xunit;

namespace Fcg.Payments.Api.Tests.Domain.Messaging
{
    public class PaymentProcessedMessageTests
    {
        [Fact]
        public void PaymentProcessedMessage_SerializesToJson_WithCamelCase()
        {
            // Arrange
            var message = new PaymentProcessedMessage(
                PaymentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                OrderId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                UserId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
                GameId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Status: "Succeeded",
                Amount: 99.99m,
                Currency: "BRL",
                ProcessedAt: new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                CorrelationId: Guid.Parse("55555555-5555-5555-5555-555555555555")
            );

            // Act
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Assert
            Assert.Contains("\"paymentId\":\"11111111-1111-1111-1111-111111111111\"", json);
            Assert.Contains("\"orderId\":\"22222222-2222-2222-2222-222222222222\"", json);
            Assert.Contains("\"userId\":\"33333333-3333-3333-3333-333333333333\"", json);
            Assert.Contains("\"gameId\":\"44444444-4444-4444-4444-444444444444\"", json);
            Assert.Contains("\"status\":\"Succeeded\"", json);
            Assert.Contains("\"amount\":99.99", json);
            Assert.Contains("\"currency\":\"BRL\"", json);
            Assert.Contains("\"correlationId\":\"55555555-5555-5555-5555-555555555555\"", json);
        }

        [Fact]
        public void PaymentProcessedMessage_CanBeCreated_WithNullOrderIdAndCorrelationId()
        {
            // Arrange & Act
            var message = new PaymentProcessedMessage(
                PaymentId: Guid.NewGuid(),
                OrderId: null,
                UserId: Guid.NewGuid(),
                GameId: Guid.NewGuid(),
                Status: "Failed",
                Amount: 50.00m,
                Currency: "USD",
                ProcessedAt: DateTime.UtcNow,
                CorrelationId: null
            );

            // Assert
            Assert.Null(message.OrderId);
            Assert.Null(message.CorrelationId);
            Assert.Equal("Failed", message.Status);
            Assert.Equal("USD", message.Currency);
        }

        [Fact]
        public void PaymentProcessedMessage_Deserializes_FromJson()
        {
            // Arrange
            var json = @"{
                ""paymentId"": ""11111111-1111-1111-1111-111111111111"",
                ""orderId"": ""22222222-2222-2222-2222-222222222222"",
                ""userId"": ""33333333-3333-3333-3333-333333333333"",
                ""gameId"": ""44444444-4444-4444-4444-444444444444"",
                ""status"": ""Succeeded"",
                ""amount"": 99.99,
                ""currency"": ""BRL"",
                ""processedAt"": ""2024-01-15T10:30:00Z"",
                ""correlationId"": ""55555555-5555-5555-5555-555555555555""
            }";

            // Act
            var message = JsonSerializer.Deserialize<PaymentProcessedMessage>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Assert
            Assert.NotNull(message);
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), message.PaymentId);
            Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), message.OrderId);
            Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), message.UserId);
            Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), message.GameId);
            Assert.Equal("Succeeded", message.Status);
            Assert.Equal(99.99m, message.Amount);
            Assert.Equal("BRL", message.Currency);
            Assert.Equal(Guid.Parse("55555555-5555-5555-5555-555555555555"), message.CorrelationId);
        }
    }
}
