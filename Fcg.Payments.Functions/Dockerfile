# Dockerfile for Fcg.Payments.Functions (containerized .NET isolated worker)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Fcg.Payments.Functions/Fcg.Payments.Functions.csproj", "Fcg.Payments.Functions/"]
RUN dotnet restore "Fcg.Payments.Functions/Fcg.Payments.Functions.csproj"
COPY . .
WORKDIR "/src/Fcg.Payments.Functions"
RUN dotnet build "Fcg.Payments.Functions.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Fcg.Payments.Functions.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fcg.Payments.Functions.dll"]
