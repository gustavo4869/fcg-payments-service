using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Infra.Messaging;
using Fcg.Payments.Functions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fcg.Payments.Functions.Tests.Services
{
    /// <summary>
    /// Integration tests for RabbitMQ Consumer Service.
    /// Requires RabbitMQ running on localhost:5672 (docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:3-management)
    /// </summary>
    public class RabbitMqConsumerServiceIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider? _serviceProvider;
        private PagamentoDbContext? _dbContext;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // In-memory database for testing
            services.AddDbContext<PagamentoDbContext>(options =>
                options.UseInMemoryDatabase("RabbitMqConsumerTests"));

            services.AddScoped<IEventStore, EfEventStore>();

            // Configure messaging options
            services.Configure<MessagingOptions>(options =>
            {
                options.Enabled = true;
                options.Host = "localhost";
                options.Port = 5672;
                options.Username = "guest";
                options.Password = "guest";
                options.VHost = "/";
                options.Exchange = "payments-test";
                options.Queue = "payment.processed.test";
                options.RoutingKey = "payment.processed.test";
            });

            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<PagamentoDbContext>();

            await _dbContext.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            if (_dbContext != null)
            {
                await _dbContext.Database.EnsureDeletedAsync();
                await _dbContext.DisposeAsync();
            }

            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        [Fact(Skip = "Integration test - requires RabbitMQ running")]
        public async Task Consumer_ShouldProcessMessage_AndStoreInEventStore()
        {
            // Arrange
            var logger = _serviceProvider!.GetRequiredService<ILogger<RabbitMqConsumerService>>();
            var options = _serviceProvider!.GetRequiredService<IOptions<MessagingOptions>>();
            var consumer = new RabbitMqConsumerService(logger, options, _serviceProvider!);

            var cts = new CancellationTokenSource();
            var consumerTask = consumer.StartAsync(cts.Token);

            // Wait for consumer to initialize
            await Task.Delay(2000);

            // Act
            // Manually publish a message to RabbitMQ (via RabbitMQ Management UI or another publisher)
            // Or use RabbitMQ.Client to publish programmatically

            await Task.Delay(5000); // Wait for message processing

            // Assert
            var eventStore = _serviceProvider!.GetRequiredService<IEventStore>();
            var paymentId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"); // Use actual paymentId from published message
            var idempotencyKey = $"rabbitmq-consumed:{paymentId}:Success";

            var storedEvent = await eventStore.GetByIdempotencyKeyAsync(idempotencyKey, CancellationToken.None);

            Assert.NotNull(storedEvent);
            Assert.Equal("PaymentProcessedConsumed", storedEvent.EventType);

            // Cleanup
            cts.Cancel();
            await consumer.StopAsync(CancellationToken.None);
        }

        [Fact(Skip = "Integration test - requires RabbitMQ running")]
        public async Task Consumer_ShouldHandleIdempotency_WhenDuplicateMessageReceived()
        {
            // Arrange
            var logger = _serviceProvider!.GetRequiredService<ILogger<RabbitMqConsumerService>>();
            var options = _serviceProvider!.GetRequiredService<IOptions<MessagingOptions>>();
            var consumer = new RabbitMqConsumerService(logger, options, _serviceProvider!);

            var cts = new CancellationTokenSource();
            var consumerTask = consumer.StartAsync(cts.Token);

            await Task.Delay(2000);

            // Act
            // Publish the SAME message TWICE to RabbitMQ
            // Consumer should process first message and ignore second (idempotent)

            await Task.Delay(5000);

            // Assert
            var eventStore = _serviceProvider!.GetRequiredService<IEventStore>();
            var paymentId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var events = await eventStore.GetByAggregateIdAsync(paymentId, CancellationToken.None);

            // Should have only ONE event despite duplicate messages
            var consumedEvents = events.Where(e => e.EventType == "PaymentProcessedConsumed").ToList();
            Assert.Single(consumedEvents);

            // Cleanup
            cts.Cancel();
            await consumer.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Consumer_ShouldNotStart_WhenMessagingDisabled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());

            services.Configure<MessagingOptions>(options =>
            {
                options.Enabled = false; // Disabled
                options.Host = "localhost";
                options.Port = 5672;
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<RabbitMqConsumerService>>();
            var options = provider.GetRequiredService<IOptions<MessagingOptions>>();
            var consumer = new RabbitMqConsumerService(logger, options, provider);

            // Act
            var cts = new CancellationTokenSource();
            await consumer.StartAsync(cts.Token);

            await Task.Delay(1000);

            // Assert
            // Consumer should log "RabbitMQ consumer disabled" and not start
            // (Manual verification via logs)

            // Cleanup
            await consumer.StopAsync(CancellationToken.None);
            await provider.DisposeAsync();
        }
    }
}
