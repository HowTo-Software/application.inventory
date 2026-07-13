FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
WORKDIR /src
COPY ["src/ComputerInventory.Api/ComputerInventory.Api.csproj", "src/ComputerInventory.Api/"]
COPY ["src/ComputerInventory.Core/ComputerInventory.Core.csproj", "src/ComputerInventory.Core/"]
COPY ["src/ComputerInventory.Infrastructure/ComputerInventory.Infrastructure.csproj", "src/ComputerInventory.Infrastructure/"]
COPY ["src/ComputerInventory.Contracts/ComputerInventory.Contracts.csproj", "src/ComputerInventory.Contracts/"]
RUN dotnet restore "src/ComputerInventory.Api/ComputerInventory.Api.csproj"
COPY . .
WORKDIR "/src/src/ComputerInventory.Api"
RUN dotnet build "ComputerInventory.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ComputerInventory.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Default to Production so containers never accidentally expose debug info
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=publish /app/publish .
# Copy agent folder with thin client package
COPY ["src/ComputerInventory.Api/agent/", "/app/agent/"]
# Copy database migration scripts for automatic application at startup
COPY ["server/migrations/", "/app/migrations/"]
ENTRYPOINT ["dotnet", "ComputerInventory.Api.dll"]
