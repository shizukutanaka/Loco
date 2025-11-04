# Deployment and DevOps Guide

## Overview

This guide covers the complete deployment and DevOps setup for Loco, including Docker containerization, docker-compose orchestration, and GitHub Actions CI/CD pipeline.

## Table of Contents

1. [Docker Containerization](#docker-containerization)
2. [Docker Compose Setup](#docker-compose-setup)
3. [GitHub Actions CI/CD](#github-actions-cicd)
4. [Deployment Strategies](#deployment-strategies)
5. [Monitoring & Logging](#monitoring--logging)
6. [Security Best Practices](#security-best-practices)

## Docker Containerization

### Overview

The Dockerfile implements a multi-stage build process for production-grade images with:
- Alpine-based images for smaller image sizes
- Non-root user execution for security
- Health checks for container orchestration
- Performance optimizations (PublishReadyToRun, trimming)
- Security hardening

### Dockerfile Structure

**Stages:**
1. **builder** - SDK stage for compilation and testing
2. **api** - ASP.NET runtime for API service
3. **web** - ASP.NET runtime for Web UI
4. **cli** - .NET runtime for CLI service

### Building Images

#### Build Single Target
```bash
docker build -t loco-api:latest --target api .
docker build -t loco-web:latest --target web .
docker build -t loco-cli:latest --target cli .
```

#### Build All Targets
```bash
docker build --target api --target web --target cli -t loco:latest .
```

### Image Features

**Security:**
- Non-root user (locouser) running applications
- Read-only file systems where possible
- Minimal attack surface (Alpine base)

**Performance:**
- PublishReadyToRun enabled for faster startup
- Trimming removes unused code
- Alpine reduces image size by ~60%

**Observability:**
- Health checks configured
- Structured logging to stdout
- OpenTelemetry integration

### Image Sizes (Estimated)

| Image | Size | Base | Notes |
|-------|------|------|-------|
| api | ~200 MB | aspnet:8.0-alpine | Includes ASP.NET Core runtime |
| web | ~200 MB | aspnet:8.0-alpine | Web UI with ASP.NET |
| cli | ~150 MB | runtime:8.0-alpine | CLI only, no ASP.NET |
| builder | ~2 GB | sdk:8.0-alpine | Build stage, not pushed |

## Docker Compose Setup

### Services

#### Core Services
- **PostgreSQL 15** - Primary database (port 5432)
- **Redis 7** - Distributed cache (port 6379)
- **RabbitMQ 3** - Message queue (ports 5672, 15672)

#### Observability Stack
- **Elasticsearch 8.10** - Log storage & search (port 9200)
- **Kibana 8.10** - Log visualization (port 5601)
- **Prometheus** - Metrics collection (port 9090)
- **Grafana** - Metrics visualization (port 3000)
- **Jaeger** - Distributed tracing (port 16686)

#### Application Services
- **Loco API** - REST API (port 5000)
- **Loco Web** - Web UI (port 5001)
- **Loco CLI** - CLI container (for scheduled jobs)
- **Nginx** - Reverse proxy (ports 80, 443)

### Quick Start

#### Development Environment
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f loco-api

# Stop all services
docker-compose down

# Remove volumes (reset data)
docker-compose down -v
```

#### Production Environment
```bash
# Use production override
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Scale services
docker-compose up -d --scale loco-api=3 --scale loco-web=2
```

### Environment Variables

Create `.env` file in project root:

```env
# Environment
ENVIRONMENT=Development

# Database
POSTGRES_PASSWORD=secure_password_here
POSTGRES_DB=loco
POSTGRES_USER=loco_user

# RabbitMQ
RABBITMQ_USER=admin
RABBITMQ_PASSWORD=secure_password_here

# Grafana
GRAFANA_USER=admin
GRAFANA_PASSWORD=secure_password_here

# Application
ASPNETCORE_ENVIRONMENT=Development
API_PORT=5000
WEB_PORT=5001
```

### Health Checks

All services include health checks:

```bash
# Check service health
docker-compose ps

# Check individual service
docker exec loco-postgres pg_isready -U loco_user
docker exec loco-redis redis-cli ping
docker exec loco-rabbitmq rabbitmq-diagnostics ping
```

### Networking

Services communicate via `loco-network` bridge network:
- Internal DNS: `service-name` (e.g., `postgres:5432`)
- External access via exposed ports
- Isolated from other compose stacks

### Data Persistence

Volumes for data persistence:
- `postgres_data` - Database files
- `redis_data` - Cache data
- `elasticsearch_data` - Log storage
- `prometheus_data` - Metrics
- `grafana_data` - Grafana config

## GitHub Actions CI/CD

### Pipeline Overview

**Workflow: enhanced-ci-cd.yml**

Triggered on:
- Push to main/develop
- Pull requests to main/develop
- Weekly schedule (Sunday 2 AM UTC)

### Jobs

#### 1. Code Quality Analysis
```yaml
- Build application
- Run code style analysis
- SonarCloud scan (main branch only)
```

Status: ✅ All checks passed

#### 2. Unit & Integration Tests
```yaml
- Run xUnit tests
- Generate code coverage
- Upload to Codecov
```

Runs in parallel across:
- Loco.Core.Tests
- Loco.Api.Tests
- Loco.Cli.Tests

#### 3. Security Scanning
```yaml
- Trivy filesystem scan
- Dependency vulnerability check
- SARIF report upload to GitHub
```

#### 4. Docker Image Build
```yaml
- Build API image
- Build Web image
- Build CLI image
- Push to ghcr.io (main branch only)
```

Builds in parallel, caches layers.

#### 5. Integration Tests
```yaml
- Start PostgreSQL service
- Start Redis service
- Run integration tests
```

With real database and cache.

#### 6. Performance Testing
```yaml
- Build benchmarks
- Run performance tests
- Comment results on PR
```

Main branch only.

#### 7. Deploy to Staging
```yaml
- Trigger after all other jobs pass
- Deploy to staging environment
- Main branch only
```

#### 8-9. Reporting & Notifications
- Build summary to GitHub
- Slack notifications on failure
- Success confirmation

### Workflow Configuration

**Triggers:**
```yaml
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  schedule:
    - cron: '0 2 * * 0'  # Weekly on Sunday
```

**Environment Variables:**
```yaml
env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}
  DOTNET_VERSION: 8.0.x
```

### Required Secrets

Configure in GitHub repository settings:

```
GITHUB_TOKEN - Automatically provided
SONAR_TOKEN - SonarCloud authentication
SLACK_WEBHOOK - Slack notifications
DOCKER_REGISTRY_PASSWORD - Container registry auth
```

### Pipeline Matrix Strategy

Tests run in parallel:
```yaml
strategy:
  matrix:
    test-project:
      - tests/Loco.Core.Tests
      - tests/Loco.Api.Tests
      - tests/Loco.Cli.Tests
```

Docker builds in parallel:
```yaml
strategy:
  matrix:
    target: [api, web, cli]
```

### Viewing Results

**GitHub UI:**
```
Actions tab → Select workflow run → View details
```

**Command Line:**
```bash
gh run list
gh run view <run-id>
gh run view <run-id> --log
```

## Deployment Strategies

### Strategy 1: Docker Compose (Development)

**Best for:** Local development, testing

```bash
docker-compose up -d
# Access: http://localhost:5000 (API)
#         http://localhost:5001 (Web)
```

### Strategy 2: Docker Compose Stack (Staging)

**Best for:** Staging environment

```bash
docker-compose -f docker-compose.yml \
                -f docker-compose.prod.yml \
                -f docker-compose.monitoring.yml \
                up -d

# With scaling
docker-compose up -d --scale loco-api=3
```

### Strategy 3: Kubernetes (Production)

**Best for:** Large-scale production

```bash
# Build and push images
docker build -t loco-api:v1.0.0 --target api .
docker push ghcr.io/org/loco/api:v1.0.0

# Deploy with Helm
helm install loco ./helm/loco \
  --set image.api.tag=v1.0.0 \
  --set replicas.api=3 \
  --namespace production
```

### Strategy 4: Cloud Platforms

#### AWS (ECS/Fargate)
```bash
# Push to ECR
aws ecr get-login-password | docker login --username AWS --password-stdin <account>.dkr.ecr.<region>.amazonaws.com

docker tag loco-api:latest <account>.dkr.ecr.<region>.amazonaws.com/loco-api:latest
docker push <account>.dkr.ecr.<region>.amazonaws.com/loco-api:latest

# Deploy with CloudFormation or Terraform
```

#### Azure (Container Instances/App Service)
```bash
# Push to ACR
az acr build --registry locoregistry --image loco-api:v1.0.0 .

# Deploy to App Service
az webapp create --resource-group loco-rg \
                 --plan loco-plan \
                 --name loco-api \
                 --deployment-container-image-name locoregistry.azurecr.io/loco-api:v1.0.0
```

#### Google Cloud (Cloud Run)
```bash
# Build with Cloud Build
gcloud builds submit --tag gcr.io/PROJECT_ID/loco-api:v1.0.0

# Deploy to Cloud Run
gcloud run deploy loco-api \
  --image gcr.io/PROJECT_ID/loco-api:v1.0.0 \
  --platform managed \
  --region us-central1
```

## Monitoring & Logging

### Logging (Serilog)

All services automatically log to:
- **Console** - Real-time monitoring
- **File** - Daily rolling text logs
- **JSON File** - Structured logs for analysis

**View logs:**
```bash
# Live logs from docker-compose
docker-compose logs -f loco-api

# View structured logs
cat Logs/loco-json-*.txt | jq .

# Filter by level
cat Logs/loco-json-*.txt | jq 'select(.Level=="Error")'

# Search by correlation ID
cat Logs/loco-json-*.txt | jq 'select(.Properties.CorrelationId=="abc-123")'
```

### Metrics (Prometheus/Grafana)

**Access Grafana:**
```
http://localhost:3000
Default: admin / admin
```

**Metrics collected:**
- HTTP request metrics
- Database query performance
- Cache hit/miss rates
- Job execution times
- Exception rates

### Distributed Tracing (Jaeger)

**Access Jaeger UI:**
```
http://localhost:16686
```

**Trace workflow execution:**
1. Navigate to service (loco-api)
2. Select operation (WorkflowExecution)
3. View spans and timings

### Log Aggregation (Elasticsearch/Kibana)

**Access Kibana:**
```
http://localhost:5601
```

**Create index pattern:**
1. Management → Index Patterns → Create index pattern
2. Pattern: `loco-*`
3. Time field: `@timestamp`

**Query logs:**
```
level: "Error"
service: "loco-api"
status: 500
```

## Security Best Practices

### Container Security

✅ **Implemented:**
- Non-root user execution
- Read-only root filesystem where possible
- No secrets in images
- Minimal base images (Alpine)
- Health checks enabled

⚠️ **Additional Hardening:**
```dockerfile
# Add to Dockerfile for production
RUN echo 'loco:x:1001:1001:Loco User:/app:/sbin/nologin' >> /etc/passwd
USER 1001
```

### Network Security

✅ **Implemented:**
- Internal network isolation
- Explicit port exposure
- Health checks with timeouts

⚠️ **Additional Hardening:**
```yaml
# docker-compose.yml
networks:
  loco-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16
```

### Secrets Management

❌ **Never do:**
```dockerfile
# BAD - Secrets in image
ENV DATABASE_PASSWORD=secretpass
```

✅ **Do this:**
```bash
# Use environment variables or secret files
docker-compose --env-file .env.prod up

# Or with Docker secrets
docker secret create db_password .env
```

### Image Security

```bash
# Scan images for vulnerabilities
trivy image ghcr.io/loco/api:latest

# Sign images
docker trust key load /path/to/key
docker trust signer add --key /path/to/key.pub loco-team ghcr.io/loco/api

# Push signed image
docker push ghcr.io/loco/api:latest
```

### Registry Security

**Private Registry:**
```bash
# Use private GitHub Container Registry
docker login ghcr.io -u USERNAME -p TOKEN

docker tag loco-api:latest ghcr.io/ORG/loco-api:latest
docker push ghcr.io/ORG/loco-api:latest
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker-compose logs loco-api

# Check health
docker-compose ps

# Inspect container
docker inspect loco-api
```

### Dependency Issues

```bash
# Check service connectivity
docker-compose exec loco-api curl -f http://postgres:5432

# Verify health checks
docker-compose exec postgres pg_isready -U loco_user
```

### Performance Issues

```bash
# Monitor resource usage
docker stats

# Check logs for errors
docker-compose logs --tail=100 loco-api | grep -i error

# Analyze with Prometheus
# http://localhost:9090
```

## Summary

The deployment infrastructure provides:

✅ Production-grade Docker images
✅ Complete docker-compose orchestration
✅ Comprehensive CI/CD pipeline
✅ Multi-environment deployment strategies
✅ Observability with ELK + Prometheus + Jaeger
✅ Security hardening and best practices
✅ Automated testing and quality gates

This enables reliable, scalable, and observable deployment of Loco across all environments.
