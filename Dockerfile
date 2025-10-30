# Multi-stage build for Loco CLI application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files
COPY *.sln .
COPY src/Loco.Cli/*.csproj ./src/Loco.Cli/
COPY src/Loco.Core/*.csproj ./src/Loco.Core/
COPY src/Loco.Api/*.csproj ./src/Loco.Api/
COPY src/Loco.Web/*.csproj ./src/Loco.Web/
COPY tests/Loco.Core.Tests/*.csproj ./tests/Loco.Core.Tests/
COPY tests/Loco.Cli.Tests/*.csproj ./tests/Loco.Cli.Tests/
COPY tests/Loco.Api.Tests/*.csproj ./tests/Loco.Api.Tests/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY . .

# Build
RUN dotnet build -c Release --no-restore

# Run tests
RUN dotnet test -c Release --no-build --verbosity normal

# Publish CLI app
RUN dotnet publish src/Loco.Cli/Loco.Cli.csproj -c Release -o /app/cli --no-restore

# Publish API app
RUN dotnet publish src/Loco.Api/Loco.Api.csproj -c Release -o /app/api --no-restore

# Publish Web app
RUN dotnet publish src/Loco.Web/Loco.Web.csproj -c Release -o /app/web --no-restore

# CLI runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS cli
WORKDIR /app
COPY --from=build /app/cli .
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Loco.Cli.dll"]

# API runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=build /app/api .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1
ENTRYPOINT ["dotnet", "Loco.Api.dll"]

# Web runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web
WORKDIR /app
COPY --from=build /app/web .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1
ENTRYPOINT ["dotnet", "Loco.Web.dll"]