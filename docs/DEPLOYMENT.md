# Deployment Guide - Self-Hosting Loco Automation Platform

> **Solves Issue #12**: Cost barriers - Self-hosted solution with Docker/Kubernetes deployment
>
> Based on 2024/2025 research:
> - Kubernetes automation tools (Plural, Rancher, Argo CD, Flux)
> - Docker containerization best practices
> - GitOps frameworks for continuous delivery
> - Self-hosted observability (Langfuse pattern)
> - Platform Engineering and DevSecOps trends (2025)

## Table of Contents

- [Quick Start](#quick-start)
- [Docker Deployment](#docker-deployment)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Cloud Providers](#cloud-providers)
- [Configuration](#configuration)
- [Monitoring](#monitoring)
- [Security](#security)
- [Cost Analysis](#cost-analysis)

## Quick Start

### Prerequisites

- **Docker** 24.0+ or **Kubernetes** 1.28+
- **Git** 2.40+
- **.NET 8.0 SDK** (for building from source)
- **PostgreSQL** 15+ or **SQLite** (for persistence)

### 5-Minute Setup (Docker Compose)

```bash
# Clone the repository
git clone https://github.com/yourusername/loco.git
cd loco

# Start all services with Docker Compose
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f loco-api

# Access the API
curl http://localhost:5000/health
```

That's it! Loco is now running at `http://localhost:5000`

## Docker Deployment

### Single Container Deployment

#### 1. Build Docker Image

```dockerfile
# Dockerfile is included in the repository
docker build -t loco:latest .
```

#### 2. Run Container

```bash
# Run with SQLite (simplest)
docker run -d \
  --name loco \
  -p 5000:5000 \
  -v loco-data:/app/data \
  loco:latest

# Run with PostgreSQL
docker run -d \
  --name loco \
  -p 5000:5000 \
  -e DATABASE_TYPE=postgresql \
  -e DATABASE_CONNECTION_STRING="Host=postgres;Database=loco;Username=loco;Password=yourpassword" \
  --link postgres:postgres \
  loco:latest
```

#### 3. Verify Deployment

```bash
docker logs loco
curl http://localhost:5000/health
```

### Docker Compose Deployment (Recommended)

```yaml
# docker-compose.yml
version: '3.8'

services:
  loco-api:
    image: loco:latest
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DATABASE_TYPE=postgresql
      - DATABASE_CONNECTION_STRING=Host=postgres;Database=loco;Username=loco;Password=yourpassword
      - LOGGING_LEVEL=Information
      - TELEMETRY_ENABLED=true
      - JAEGER_ENDPOINT=http://jaeger:4317
    depends_on:
      - postgres
      - jaeger
    volumes:
      - loco-workflows:/app/workflows
      - loco-logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  postgres:
    image: postgres:15-alpine
    environment:
      - POSTGRES_DB=loco
      - POSTGRES_USER=loco
      - POSTGRES_PASSWORD=yourpassword
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # Jaeger UI
      - "4317:4317"    # OTLP gRPC
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    restart: unless-stopped

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
    depends_on:
      - prometheus
    restart: unless-stopped

volumes:
  postgres-data:
  loco-workflows:
  loco-logs:
  prometheus-data:
  grafana-data:
```

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `DATABASE_TYPE` | Database type (`sqlite`, `postgresql`) | `sqlite` | No |
| `DATABASE_CONNECTION_STRING` | Connection string for PostgreSQL | - | If PostgreSQL |
| `LOGGING_LEVEL` | Logging level (`Debug`, `Information`, `Warning`, `Error`) | `Information` | No |
| `TELEMETRY_ENABLED` | Enable OpenTelemetry | `false` | No |
| `JAEGER_ENDPOINT` | Jaeger collector endpoint | - | If telemetry enabled |
| `ENCRYPTION_KEY` | Master encryption key for workflows | - | Yes (production) |
| `API_PORT` | API listening port | `5000` | No |

## Kubernetes Deployment

### Using Kubectl (Manual)

#### 1. Create Namespace

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: loco
```

```bash
kubectl apply -f namespace.yaml
```

#### 2. Deploy Database

```yaml
# postgres-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
  namespace: loco
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:15-alpine
        env:
        - name: POSTGRES_DB
          value: "loco"
        - name: POSTGRES_USER
          value: "loco"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: loco
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
```

#### 3. Deploy Loco API

```yaml
# loco-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: loco-api
  namespace: loco
spec:
  replicas: 3
  selector:
    matchLabels:
      app: loco-api
  template:
    metadata:
      labels:
        app: loco-api
    spec:
      containers:
      - name: loco-api
        image: loco:latest
        env:
        - name: DATABASE_TYPE
          value: "postgresql"
        - name: DATABASE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: loco-secret
              key: db-connection-string
        - name: ENCRYPTION_KEY
          valueFrom:
            secretKeyRef:
              name: loco-secret
              key: encryption-key
        ports:
        - containerPort: 5000
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 5
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: loco-api
  namespace: loco
spec:
  type: LoadBalancer
  selector:
    app: loco-api
  ports:
  - port: 80
    targetPort: 5000
```

#### 4. Apply Configurations

```bash
kubectl apply -f postgres-deployment.yaml
kubectl apply -f loco-deployment.yaml

# Check status
kubectl get pods -n loco
kubectl get svc -n loco
```

### Using Helm (Recommended)

```bash
# Add Loco Helm repository
helm repo add loco https://charts.loco.dev
helm repo update

# Install Loco
helm install loco loco/loco \
  --namespace loco \
  --create-namespace \
  --set database.type=postgresql \
  --set database.password=yourpassword \
  --set encryption.key=your-encryption-key \
  --set replicaCount=3

# Upgrade
helm upgrade loco loco/loco \
  --namespace loco \
  --set replicaCount=5

# Uninstall
helm uninstall loco --namespace loco
```

### GitOps with Argo CD

```yaml
# argocd-application.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: loco
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/yourusername/loco
    targetRevision: HEAD
    path: k8s
  destination:
    server: https://kubernetes.default.svc
    namespace: loco
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
    - CreateNamespace=true
```

## Cloud Providers

### AWS (ECS/EKS)

#### ECS Fargate

```bash
# Create ECS cluster
aws ecs create-cluster --cluster-name loco-cluster

# Register task definition
aws ecs register-task-definition --cli-input-json file://ecs-task-definition.json

# Create service
aws ecs create-service \
  --cluster loco-cluster \
  --service-name loco-api \
  --task-definition loco-api:1 \
  --desired-count 3 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-12345],securityGroups=[sg-12345],assignPublicIp=ENABLED}"
```

#### EKS

```bash
# Create EKS cluster
eksctl create cluster \
  --name loco-cluster \
  --region us-west-2 \
  --nodegroup-name standard-workers \
  --node-type t3.medium \
  --nodes 3

# Deploy to EKS
kubectl apply -f k8s/
```

### Google Cloud (GKE)

```bash
# Create GKE cluster
gcloud container clusters create loco-cluster \
  --zone us-central1-a \
  --num-nodes 3 \
  --machine-type n1-standard-2

# Get credentials
gcloud container clusters get-credentials loco-cluster --zone us-central1-a

# Deploy
kubectl apply -f k8s/
```

### Azure (AKS)

```bash
# Create resource group
az group create --name loco-rg --location eastus

# Create AKS cluster
az aks create \
  --resource-group loco-rg \
  --name loco-cluster \
  --node-count 3 \
  --enable-addons monitoring \
  --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group loco-rg --name loco-cluster

# Deploy
kubectl apply -f k8s/
```

## Configuration

### Database

#### PostgreSQL (Recommended for Production)

```bash
# Connection string format
Host=localhost;Port=5432;Database=loco;Username=loco;Password=yourpassword;SSL Mode=Require
```

#### SQLite (Development/Single Instance)

```bash
# Data source format
Data Source=/app/data/loco.db
```

### Security

#### Generate Encryption Key

```bash
# Generate 32-byte random key
openssl rand -base64 32
```

#### Kubernetes Secrets

```bash
# Create secrets
kubectl create secret generic loco-secret \
  --from-literal=db-connection-string='Host=postgres;Database=loco;Username=loco;Password=yourpassword' \
  --from-literal=encryption-key='your-base64-encoded-key' \
  --namespace loco
```

## Monitoring

### OpenTelemetry + Jaeger

```yaml
# Configuration already included in docker-compose.yml
# Access Jaeger UI at http://localhost:16686
```

### Prometheus + Grafana

```yaml
# prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'loco-api'
    static_configs:
      - targets: ['loco-api:5000']
```

Access Grafana at `http://localhost:3000` (admin/admin)

### Health Checks

```bash
# Check API health
curl http://localhost:5000/health

# Expected response
{
  "status": "healthy",
  "version": "1.0.0",
  "checks": {
    "database": "healthy",
    "disk_space": "healthy"
  }
}
```

## Cost Analysis

### Self-Hosted vs SaaS Comparison

| Deployment | Monthly Cost | Notes |
|------------|--------------|-------|
| **Single Docker Container** | **$5-20** | VPS (DigitalOcean, Linode, Hetzner) |
| **Docker Compose (3 services)** | **$20-40** | 2 vCPU, 4GB RAM VPS |
| **Kubernetes (3 nodes)** | **$60-150** | AWS EKS, GKE, or AKS |
| **Zapier Pro** | $19.99/month | Limited to 750 tasks |
| **Make.com** | $9-29/month | Limited operations |
| **Power Automate** | $15/user/month | Microsoft 365 integration |

### Cost Savings Examples

#### Small Team (5 users)

- **Loco (Self-Hosted)**: $20/month (Docker Compose on VPS)
- **Zapier**: $99.99/month (5 users × $19.99)
- **Annual Savings**: $959.88 (96% reduction)

#### Medium Team (20 users)

- **Loco (Self-Hosted)**: $60/month (Kubernetes on cloud)
- **Power Automate**: $300/month (20 users × $15)
- **Annual Savings**: $2,880 (80% reduction)

#### Enterprise (100+ users)

- **Loco (Self-Hosted)**: $150/month (Kubernetes auto-scaling)
- **UiPath**: $8,000/month (Enterprise pricing)
- **Annual Savings**: $94,200 (98% reduction)

## Security Best Practices

### 1. Use HTTPS/TLS

```yaml
# nginx reverse proxy with Let's Encrypt
services:
  nginx:
    image: nginx:alpine
    ports:
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - /etc/letsencrypt:/etc/letsencrypt
```

### 2. Enable Authentication

```bash
# Set API key for authentication
docker run -d \
  -e API_KEY=your-secret-api-key \
  loco:latest
```

### 3. Network Isolation

```yaml
# Docker Compose with internal network
networks:
  loco-internal:
    driver: bridge
    internal: true
```

### 4. Regular Backups

```bash
# Backup PostgreSQL
docker exec postgres pg_dump -U loco loco > backup-$(date +%Y%m%d).sql

# Restore
docker exec -i postgres psql -U loco loco < backup-20251025.sql
```

## Troubleshooting

### Common Issues

#### Port Already in Use

```bash
# Change port mapping
docker run -p 8080:5000 loco:latest
```

#### Database Connection Failed

```bash
# Check database logs
docker logs postgres

# Verify connection
docker exec -it postgres psql -U loco -d loco
```

#### Out of Memory

```bash
# Increase container memory limit
docker run -m 1g loco:latest
```

## Next Steps

- [Configuration Guide](CONFIGURATION.md)
- [API Documentation](API_REFERENCE.md)
- [Monitoring Setup](../examples/observability-example.cs)
- [Security Guide](SECURITY_GUIDE.md)
- [Contributing](../CONTRIBUTING.md)

## Support

- GitHub Issues: https://github.com/yourusername/loco/issues
- Documentation: https://docs.loco.dev
- Community Forum: https://community.loco.dev
