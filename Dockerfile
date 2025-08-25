# syntax=docker/dockerfile:1
# Multi-stage build for FKS Ninja (net48 library) targeting NinjaTrader 8.
# NOTE: .NET Framework 4.8 is Windows-only; this Linux container uses Mono
# to compile for packaging/distribution (testing runtime UI not supported here).

FROM mono:6.12 AS build
WORKDIR /src
COPY src/ ./src/
COPY references/ ./references/
RUN mkdir -p packages || true
COPY packages/ ./packages/
COPY manifest.xml ./
RUN sed -i 's|deb.debian.org/debian|archive.debian.org/debian|g; s|deb.debian.org/debian-security|archive.debian.org/debian-security|g' /etc/apt/sources.list \
    && sed -i '/security.debian.org/d' /etc/apt/sources.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/* \
    && curl -L -o /usr/local/bin/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe \
    && mono /usr/local/bin/nuget.exe restore src/FKS.csproj || echo "NuGet restore failed or partially completed (continuing)" \
    && msbuild src/FKS.csproj /p:Configuration=Release || \
    (echo "Build failed. Ensure all reference DLLs are present in references/ and internet access allowed for NuGet." && exit 1)

# Package stage creates a tarball with DLL + supporting files
FROM alpine:3.19 AS package
WORKDIR /out
COPY --from=build /src/bin/Release/ ./bin/
COPY --from=build /src/manifest.xml ./
RUN tar -czf fks_ninja_package.tgz bin || (echo "Packaging failed" && exit 1)

# Minimal artifact image (optional) just holding the compiled package
FROM busybox:1.36 AS artifact
LABEL org.opencontainers.image.title="fks_ninja" \
      org.opencontainers.image.description="Compiled FKS NinjaTrader strategy/library (net48)" \
      org.opencontainers.image.source="https://example.com/fks_ninja"
COPY --from=package /out/fks_ninja_package.tgz /fks_ninja_package.tgz
CMD ["sh", "-c", "ls -l /fks_ninja_package.tgz && echo 'Extract with: docker cp $(hostname):/fks_ninja_package.tgz .' && sleep 3600"]
