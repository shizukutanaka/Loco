# Phase 3: Multi-stage Docker Build with Optimization
# Optimized for production deployment with minimal image size (~200MB)
# Security: Non-root user, minimal base image, Alpine Linux, security scanning ready

# ============================================================================
# Stage 1: Build stage (SDK with build tools)
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /build

# Install build dependencies (minimal)
RUN apk add --no-cache git curl gnupg

# Copy solution and project files
COPY ["*.sln", "."]
COPY ["src/Loco.Api/Loco.Api.csproj", "src/Loco.Api/"]
COPY ["src/Loco.Core/Loco.Core.csproj", "src/Loco.Core/"]
COPY ["src/Loco.VisualEditor/Loco.VisualEditor.csproj", "src/Loco.VisualEditor/"]

# Restore dependencies (layer caching for faster builds)
RUN dotnet restore "src/Loco.Api/Loco.Api.csproj" \
    && dotnet restore "src/Loco.Core/Loco.Core.csproj" \
    && dotnet restore "src/Loco.VisualEditor/Loco.VisualEditor.csproj"

# Copy source code
COPY . .

# Build (with Release configuration for optimization)
RUN dotnet build "src/Loco.Api/Loco.Api.csproj" \
    -c Release \
    -o /build/output \
    --no-restore \
    --verbosity minimal

# Publish with aggressive optimizations
# PublishTrimmed: Remove unused code (20-30% size reduction)
# PublishReadyToRun: Precompile IL to native (faster startup)
# TrimMode=link: Link-time trimming for better optimization
RUN dotnet publish "src/Loco.Api/Loco.Api.csproj" \
    -c Release \
    -o /build/publish \
    --no-restore \
    --no-build \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    --self-contained

# ============================================================================
# Stage 2: Runtime stage (minimal Alpine runtime image)
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install only necessary runtime dependencies
RUN apk add --no-cache \
    ca-certificates \
    curl \
    tini \
    sqlite-libs \
    && rm -rf /var/cache/apk/*

# Create non-root user for security (prevents privilege escalation)
# UID 1000, GID 1000 (standard non-root user)
RUN addgroup -g 1000 appuser && \
    adduser -D -u 1000 -G appuser appuser

# Copy published files from build stage (reduce layer size)
COPY --from=build --chown=appuser:appuser /build/publish .
COPY --from=build --chown=appuser:appuser /build/src/Loco.Api/appsettings*.json ./

# Health check (responds to liveness probes)
# 30s interval, 3s timeout, 5s initial delay, 3 retries before unhealthy
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# ============================================================================
# Runtime Configuration - Production Optimizations
# ============================================================================
# ASP.NET Core
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# .NET Runtime Optimizations
# Tiered JIT compilation for faster startup
ENV DOTNET_TieredCompilation=1
# Quick JIT for faster initial compilation
ENV DOTNET_TieredCompilationQuickJit=1
# Thread pool minimum
ENV DOTNET_ThreadPool_MinThreads=4
# Disable ETW tracing (security, performance)
ENV COMPlus_EnableDiagnostics=0

# GC Optimization
# Server GC mode
ENV DOTNET_GCServer=1
# Single heap for container
ENV DOTNET_GCHeapCount=1
# Affinitize to CPU
ENV DOTNET_GCHeapAffinitizeMask=1

# Container Resource Limits (Kubernetes sets these)
# Use 80% of memory limit
ENV DOTNET_GCHeapHardLimitPercent=80

# Security
ENV ASPNETCORE_Urls=http://+:5000
ENV ASPNETCORE_ForwardedHeadersEnabled=true

# ============================================================================
# Security & Metadata
# ============================================================================
# Switch to non-root user (security best practice)
USER appuser

# Use tini as init process (proper signal handling for graceful shutdown)
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["dotnet", "Loco.Api.dll"]

# Expose HTTP port (5000)
# Note: EXPOSE is informational only, doesn't actually open the port
EXPOSE 5000

# OCI Image Labels for metadata
LABEL org.opencontainers.image.vendor="Loco"
LABEL org.opencontainers.image.title="Loco Workflow Automation Engine"
LABEL org.opencontainers.image.description="Enterprise-grade lightweight workflow automation platform - Phase 3"
LABEL org.opencontainers.image.version="3.0.0"
LABEL org.opencontainers.image.source="https://github.com/your-org/loco"
LABEL org.opencontainers.image.documentation="https://docs.loco.local/api"
LABEL org.opencontainers.image.vendor.url="https://loco.local"

# ============================================================================
# Build Targets
# ============================================================================
# Build command: docker build -t loco:3.0.0 .
# Size estimate: ~200-250MB (Alpine + trimmed .NET)
# Security scan: trivy image loco:3.0.0
# ============================================================================