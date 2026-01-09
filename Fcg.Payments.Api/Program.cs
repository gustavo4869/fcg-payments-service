using Fcg.Payments.Api.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiCore(builder.Configuration);

var app = builder.Build();

app.UseApiCore();
app.MapV1Endpoints();

app.Run();