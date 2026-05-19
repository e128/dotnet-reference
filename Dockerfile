# =============================================================================
# Multi-stage Dockerfile for E128.Reference.Web
#
# FIPS 140-2 compliant: Noble (OpenSSL 3.x FIPS provider) replaces Alpine
# (LibreSSL, no FIPS validation path).
#
# Build:   docker build -t e128-reference-web .
# Run:     docker run -p 8080:8080 e128-reference-web
# =============================================================================

# --- Stage 1: Restore ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS restore
WORKDIR /src

# Copy build infrastructure first (maximizes layer caching)
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props nuget.config .globalconfig .editorconfig global.json ./

# Copy project files for restore (Web + Core dependency only)
COPY src/E128.Reference.Core/E128.Reference.Core.csproj src/E128.Reference.Core/
COPY src/E128.Reference.Web/E128.Reference.Web.csproj src/E128.Reference.Web/

RUN dotnet restore src/E128.Reference.Web/E128.Reference.Web.csproj

# --- Stage 2: Build ---
FROM restore AS build
WORKDIR /src

COPY src/ src/

RUN dotnet publish src/E128.Reference.Web/E128.Reference.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# --- Stage 3: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime

# FIPS 140-2: Noble ships OpenSSL 3.x whose FIPS provider is validated.
# 1. Install openssl CLI (needed for fipsinstall) and curl (healthcheck).
# 2. Generate the FIPS module integrity file via fipsinstall.
# 3. Write an OpenSSL config that activates the FIPS provider and sets
#    default_properties = fips=yes so non-FIPS algorithms fail at runtime.
# 4. Remove the openssl CLI (no longer needed) and clean apt caches.
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl openssl && \
    FIPS_MODULE="$(find /usr/lib -name 'fips.so' -type f | head -1)" && \
    openssl fipsinstall \
        -out /etc/ssl/fipsmodule.cnf \
        -module "${FIPS_MODULE}" && \
    printf '%s\n' \
        'config_diagnostics = 1' \
        'openssl_conf = openssl_init' \
        '' \
        '.include /etc/ssl/fipsmodule.cnf' \
        '' \
        '[openssl_init]' \
        'providers = provider_sect' \
        'alg_section = algorithm_sect' \
        '' \
        '[provider_sect]' \
        'fips = fips_sect' \
        'default = default_sect' \
        '' \
        '[default_sect]' \
        'activate = 1' \
        '' \
        '[fips_sect]' \
        'activate = 1' \
        '' \
        '[algorithm_sect]' \
        'default_properties = fips=yes' \
        > /etc/ssl/openssl-fips.cnf && \
    apt-get purge -y --auto-remove openssl && \
    rm -rf /var/lib/apt/lists/* /var/cache/apt/*

# .NET uses OpenSSL on Linux; this config forces the FIPS provider
ENV OPENSSL_CONF=/etc/ssl/openssl-fips.cnf

WORKDIR /app

# Non-root user (APP_UID provided by Microsoft .NET images)
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E128.Reference.Web.dll"]
