using Fcg.Payments.Api.Domain.Messaging;
using Fcg.Payments.Api.Infra.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fcg.Payments.Api.Tests.Infra.Messaging
{
    public class NoOpPaymentEventPublisherTests
    {
        [Fact]
        public async Task PublishPaymentProcessedAsync_ShouldComplete_WithoutException()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<NoOpPaymentEventPublisher>>();
            var publisher = new NoOpPaymentEventPublisher(loggerMock.Object);
            
            var message = new PaymentProcessedMessage(
                PaymentId: Guid.NewGuid(),
                OrderId: null,
                UserId: Guid.NewGuid(),
                GameId: Guid.NewGuid(),
                Status: "Succeeded",
                Amount: 100.00m,
                Currency: "BRL",
                ProcessedAt: DateTime.UtcNow,
                CorrelationId: null
            );

            // Act & Assert
            await publisher.PublishPaymentProcessedAsync(message);
            
            // Verify debug log was called
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Messaging disabled")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishPaymentProcessedAsync_ShouldNotThrow_WithNullValues()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<NoOpPaymentEventPublisher>>();
            var publisher = new NoOpPaymentEventPublisher(loggerMock.Object);
            
            var message = new PaymentProcessedMessage(
                PaymentId: Guid.NewGuid(),
                OrderId: null,
                UserId: Guid.Empty,
                GameId: Guid.Empty,
                Status: "Failed",
                Amount: 0m,
                Currency: "",
                ProcessedAt: DateTime.MinValue,
                CorrelationId: null
            );

            // Act & Assert - Should not throw
            await publisher.PublishPaymentProcessedAsync(message);
        }
    }
}
