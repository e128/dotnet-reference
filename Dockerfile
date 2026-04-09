# =============================================================================
# Multi-stage Dockerfile for E128.Reference.Web
#
# Build:   docker build -t e128-reference-web .
# Run:     docker run -p 8080:8080 e128-reference-web
# =============================================================================

# --- Stage 1: Restore ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy build infrastructure first (maximizes layer caching)
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props nuget.config .globalconfig .editorconfig global.json ./
COPY E128.Reference.slnx ./

# Copy project files for restore
COPY src/E128.Reference.Core/E128.Reference.Core.csproj src/E128.Reference.Core/
COPY src/E128.Reference.Web/E128.Reference.Web.csproj src/E128.Reference.Web/

RUN dotnet restore E128.Reference.slnx --runtime linux-x64

# --- Stage 2: Build ---
FROM restore AS build
WORKDIR /src

# Copy source code
COPY src/ src/

RUN dotnet publish src/E128.Reference.Web/E128.Reference.Web.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --no-restore \
    --output /app/publish

# --- Stage 3: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user for security
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E128.Reference.Web.dll"]
