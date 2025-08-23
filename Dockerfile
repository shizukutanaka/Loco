# Multi-stage build for minimal image size
# Following John Carmack's efficiency principles

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files
COPY Directory.Build.props .
COPY src/Loco.Core/Loco.Core.csproj src/Loco.Core/
COPY src/Loco.Web/Loco.Web.csproj src/Loco.Web/

# Restore dependencies
RUN dotnet restore src/Loco.Web/Loco.Web.csproj -r linux-musl-x64

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/Loco.Web/Loco.Web.csproj \
    -c Release \
    -r linux-musl-x64 \
    -o /app/publish \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:EnableCompressionInSingleFile=true

# Runtime stage - minimal Alpine image
FROM alpine:latest
WORKDIR /app

# Install required runtime dependencies
RUN apk add --no-cache \
    wget \
    libstdc++ \
    libgcc \
    icu-libs \
    && rm -rf /var/cache/apk/*

# Copy published app
COPY --from=build /app/publish/Loco.Web /app/loco-web

# Create non-root user
RUN adduser -D -s /bin/sh loco && \
    chown -R loco:loco /app

USER loco

# Expose port for web service
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://localhost:8080/healthz || exit 1

# Entry point
ENTRYPOINT ["/app/loco-web"]
