using Fcg.Payments.Api.Api.Middleware;
using Fcg.Payments.Api.Application.Pagamentos;
using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra;
using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Infra.HostedServices;
using Fcg.Payments.Api.Infra.Repositorio;
using FluentValidation;
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
            var connectionString = cfg.GetConnectionString("DefaultConnection") ?? "Data Source=fcg.db";
            var jwtKey = cfg["Jwt:Key"]; // read early so swagger can be configured

            services.AddDbContext<PagamentoDbContext>(o => o.UseSqlite(connectionString));

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
