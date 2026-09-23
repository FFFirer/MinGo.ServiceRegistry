# Multi-stage build for the MinGo Service Registry Server (net10.0).
# Build context is the repository root:
#   docker build -f deploy/docker/Dockerfile -t mingo/service-registry:0.1.0 .

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG CONFIGURATION=Release
# Optional extra NuGet feed hosting MinGo.ServiceRegistry.Abstractions (for example a local feed
# or GitHub Packages). Leave empty to restore from nuget.org only (already in nuget.config).
ARG EXTRA_NUGET_SOURCE=
WORKDIR /source

# Copy repo-root MSBuild infrastructure and source (better layer caching than copying everything).
COPY nuget.config Directory.Build.props Directory.Packages.props version.props ./
COPY src/ ./src/

# Register the extra feed only when provided.
RUN if [ -n "$EXTRA_NUGET_SOURCE" ]; then \
        dotnet nuget add source "$EXTRA_NUGET_SOURCE" --name extra --configfile /source/nuget.config; \
    fi

RUN dotnet restore src/MinGo.ServiceRegistry.Server/MinGo.ServiceRegistry.Server.csproj

RUN dotnet publish src/MinGo.ServiceRegistry.Server/MinGo.ServiceRegistry.Server.csproj \
        -c "$CONFIGURATION" -o /app --no-restore /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Bind all interfaces on 8080 inside the container (appsettings.json carries no Urls key,
# so this environment variable is the effective listen address).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the non-root user baked into the aspnet image.
USER app

ENTRYPOINT ["dotnet", "MinGo.ServiceRegistry.Server.dll"]
