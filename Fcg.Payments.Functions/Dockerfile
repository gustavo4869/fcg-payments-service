# Use Azure Functions .NET isolated base for proper Functions host
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore
COPY ["Fcg.Payments.Functions/Fcg.Payments.Functions.csproj", "Fcg.Payments.Functions/"]
RUN dotnet restore "Fcg.Payments.Functions/Fcg.Payments.Functions.csproj"

# copy everything and publish
COPY . .
WORKDIR "/src/Fcg.Payments.Functions"
RUN dotnet publish "Fcg.Payments.Functions.csproj" -c Release -o /out /p:UseAppHost=false

# Use Azure Functions base image that runs the Functions host and the isolated worker
# Use Functions runtime 4 tag which is available on MCR
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4 AS runtime
WORKDIR /home/site/wwwroot

# Copy published output to the function app root expected by the host
COPY --from=build /out .

# Ensure Functions host finds the app
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
# Ensure the app listens on port 80 so Container Apps targetPort matches the container
ENV ASPNETCORE_URLS="http://0.0.0.0:80"

# The base image's entrypoint will run the Functions host.
