# Multi-stage Dockerfile extending shared .NET template

# Build stage - extends shared .NET template
FROM shared/dotnet:9.0 AS build

WORKDIR /src

# Copy solution and project files for dependency caching
COPY *.sln global.json* nuget.config* ./
COPY src/*/*.csproj ./
RUN dotnet restore --verbosity minimal

# Copy source code and build
COPY src ./src
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/FKSNinja -c Release -o /app/publish \
    --no-restore --verbosity minimal

# Runtime stage - using ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application from build stage
COPY --from=build /app/publish .

# Set service-specific environment variables
ENV SERVICE_NAME=fks-ninja \
    SERVICE_TYPE=ninja \
    SERVICE_PORT=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:${SERVICE_PORT}/health || exit 1

EXPOSE ${SERVICE_PORT}

# Create non-root user
ARG USER_ID=1088
RUN groupadd --gid ${USER_ID} appuser \
    && useradd --uid ${USER_ID} --gid appuser --create-home --shell /bin/bash appuser \
    && chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "FKSNinja.dll"]
