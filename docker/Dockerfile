# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY LeanCache.slnx Directory.Build.props ./
COPY tests/Directory.Build.props tests/
COPY src/LeanCache.Core/LeanCache.Core.csproj src/LeanCache.Core/
COPY src/LeanCache.Protocol/LeanCache.Protocol.csproj src/LeanCache.Protocol/
COPY src/LeanCache.Server/LeanCache.Server.csproj src/LeanCache.Server/
COPY src/LeanCache.Client/LeanCache.Client.csproj src/LeanCache.Client/
COPY src/LeanCache.Benchmark/LeanCache.Benchmark.csproj src/LeanCache.Benchmark/
COPY tests/LeanCache.Core.Tests/LeanCache.Core.Tests.csproj tests/LeanCache.Core.Tests/
COPY tests/LeanCache.Protocol.Tests/LeanCache.Protocol.Tests.csproj tests/LeanCache.Protocol.Tests/
COPY tests/LeanCache.Server.Tests/LeanCache.Server.Tests.csproj tests/LeanCache.Server.Tests/
COPY tests/LeanCache.Client.Tests/LeanCache.Client.Tests.csproj tests/LeanCache.Client.Tests/

RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish src/LeanCache.Server/LeanCache.Server.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Default cache port
EXPOSE 6379

# Health check: attempt a TCP connection to the cache port
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet LeanCache.Server.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "LeanCache.Server.dll"]
