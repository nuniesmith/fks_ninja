## Simplified .NET (C#) Dockerfile for Ninja service
## Assumes a single project FKS.csproj under src/; adjust if different.

ARG DOTNET_VERSION=8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY *.sln ./ 2>/dev/null || true
COPY src ./src
RUN dotnet restore ./src || true
RUN dotnet publish ./src -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
EXPOSE 8080
ENTRYPOINT ["dotnet","FKS.dll"]
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -qO- http://127.0.0.1:8080/ >/dev/null 2>&1 || exit 1
