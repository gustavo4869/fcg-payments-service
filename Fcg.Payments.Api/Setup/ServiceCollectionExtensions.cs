using Fcg.Payments.Api.Api.Middleware;
using Fcg.Payments.Api.Application.Pagamentos;
using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra;
using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Infra.HostedServices;
using Fcg.Payments.Api.Infra.Repositorio;
using FluentValidation;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

namespace Fcg.Payments.Api.Setup
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApiCore(this IServiceCollection services, IConfiguration cfg)
        {
            // Application Insights - Configuração completa
            // Tentar múltiplos formatos de configuração (compatibilidade com diferentes ambientes)
            var connectionString = cfg["ApplicationInsights:ConnectionString"]
                ?? cfg["ApplicationInsights__ConnectionString"] 
                ?? cfg["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                ?? Environment.GetEnvironmentVariable("ApplicationInsights__ConnectionString")
                ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

            // Log de debug para diagnóstico (será enviado ao Application Insights quando configurado)
            Console.WriteLine($"[DEBUG] Application Insights Connection String found: {!string.IsNullOrWhiteSpace(connectionString)}");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine($"[DEBUG] Connection String length: {connectionString.Length} characters");
                Console.WriteLine($"[DEBUG] Contains InstrumentationKey: {connectionString.Contains("InstrumentationKey")}");
            }
            else
            {
                Console.WriteLine("[WARNING] Application Insights Connection String NOT FOUND - Telemetry disabled");
                Console.WriteLine("[DEBUG] Checked keys: ApplicationInsights:ConnectionString, ApplicationInsights__ConnectionString, APPLICATIONINSIGHTS_CONNECTION_STRING");
            }

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = connectionString;
                    
                    // Controle de sampling
                    options.EnableAdaptiveSampling = cfg.GetValue<bool>("ApplicationInsights:EnableAdaptiveSampling", true);
                    
                    // Coleta de performance counters (CPU, memória, etc)
                    options.EnablePerformanceCounterCollectionModule = cfg.GetValue<bool>("ApplicationInsights:EnablePerformanceCounterCollectionModule", true);
                    
                    // Rastreamento de dependências (HTTP, SQL, etc)
                    options.EnableDependencyTrackingTelemetryModule = cfg.GetValue<bool>("ApplicationInsights:EnableDependencyTrackingTelemetryModule", true);
                    
                    // Coleta de heartbeat para monitoramento de health
                    options.EnableHeartbeat = true;
                    
                    // Coleta automática de requisições HTTP
                    options.EnableRequestTrackingTelemetryModule = true;
                    
                    // Coleta de eventos de exceção
                    options.EnableEventCounterCollectionModule = true;
                });
                
                // Configurar TelemetryInitializer para nome da aplicação
                services.AddSingleton<ITelemetryInitializer>(new CloudRoleNameTelemetryInitializer("fcg-payments"));
                
                // Integrar ILogger com Application Insights
                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddApplicationInsights(
                        configureTelemetryConfiguration: (config) => 
                            config.ConnectionString = connectionString,
                        configureApplicationInsightsLoggerOptions: (options) => { }
                    );
                });
                
                Console.WriteLine("[SUCCESS] Application Insights configured successfully!");
            }

            var connectionStringDb = cfg.GetConnectionString("DefaultConnection") ?? "Data Source=fcg.db";
            var jwtKey = cfg["Jwt:Key"];

            services.AddDbContext<PagamentoDbContext>(o => o.UseSqlite(connectionStringDb));

            services.AddScoped<IPagamentoRepository, PagamentoRepository>();
            services.AddScoped<IEventStore, EfEventStore>();
            services.AddValidatorsFromAssemblyContaining<CriarPagamentoValidator>();

            // register IMiddleware implementations so UseMiddleware can resolve them
            services.AddTransient<ErrorMiddleware>();
            services.AddTransient<RequestLoggingMiddleware>();

            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG Payments API", Version = "v1" });

                if (!string.IsNullOrWhiteSpace(jwtKey))
                {
                    var scheme = new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Description = "Enter 'Bearer' [space] and then your valid JWT token.",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Reference = new OpenApiReference
                        {
                            Id = JwtBearerDefaults.AuthenticationScheme,
                            Type = ReferenceType.SecurityScheme
                        }
                    };

                    options.AddSecurityDefinition("Bearer", scheme);
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        [ scheme ] = new string[] { }
                    });
                }
            });

            services.AddHealthChecks()
                .AddDbContextCheck<PagamentoDbContext>("efcore-db", failureStatus: HealthStatus.Unhealthy);

            // Authentication / Authorization (JWT)
            if (!string.IsNullOrWhiteSpace(jwtKey))
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,
                        ValidateLifetime = true,
                        RoleClaimType = ClaimTypes.Role
                    };
                });

                services.AddAuthorization(opt =>
                {
                    opt.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
                });
            }

            // Register hosted service that simulates async payment processing. Will also be useful locally when demonstrating Azure Function integration.
            services.AddHostedService<PaymentProcessorHostedService>();

            return services;
        }
    }
}
