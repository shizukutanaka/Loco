# Multi-Cloud Infrastructure as Code with Terraform
# Supports AWS, Azure, and GCP deployment

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.0"
    }
  }

  backend "s3" {
    bucket         = "loco-terraform-state"
    key            = "multicloud/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "terraform-state-lock"
  }
}

# ==================== AWS Configuration ====================

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "Loco"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  }
}

# AWS Lambda Functions for Serverless
resource "aws_lambda_function" "loco_api_functions" {
  for_each = var.lambda_functions

  function_name = "loco-${var.environment}-${each.key}"
  runtime       = "dotnet8"
  handler       = each.value.handler
  role         = aws_iam_role.lambda_execution_role.arn

  filename         = each.value.package
  source_code_hash = filebase64sha256(each.value.package)

  timeout     = each.value.timeout
  memory_size = each.value.memory

  environment {
    variables = merge(
      var.common_env_vars,
      each.value.env_vars
    )
  }

  vpc_config {
    subnet_ids         = aws_subnet.private[*].id
    security_group_ids = [aws_security_group.lambda.id]
  }

  tracing_config {
    mode = "Active"
  }

  layers = [
    aws_lambda_layer_version.loco_common.arn
  ]
}

# AWS API Gateway for REST APIs
resource "aws_apigatewayv2_api" "loco_api" {
  name          = "loco-${var.environment}-api"
  protocol_type = "HTTP"

  cors_configuration {
    allow_origins     = var.cors_origins
    allow_methods     = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
    allow_headers     = ["*"]
    expose_headers    = ["*"]
    max_age          = 3600
  }
}

# AWS EKS Cluster for Container Workloads
module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 19.0"

  cluster_name    = "loco-${var.environment}"
  cluster_version = "1.28"

  vpc_id     = aws_vpc.main.id
  subnet_ids = aws_subnet.private[*].id

  eks_managed_node_groups = {
    general = {
      desired_size = 3
      min_size     = 2
      max_size     = 10

      instance_types = ["t3.medium"]

      k8s_labels = {
        Environment = var.environment
        GithubRepo  = "loco"
      }
    }
  }
}

# AWS RDS Aurora Serverless v2 for Database
resource "aws_rds_cluster" "loco_db" {
  cluster_identifier = "loco-${var.environment}-aurora"
  engine            = "aurora-postgresql"
  engine_mode       = "provisioned"
  engine_version    = "15.3"

  database_name   = "loco"
  master_username = "loco_admin"
  master_password = random_password.db_password.result

  serverlessv2_scaling_configuration {
    max_capacity = 16
    min_capacity = 0.5
  }

  backup_retention_period = 30
  preferred_backup_window = "03:00-04:00"

  enabled_cloudwatch_logs_exports = ["postgresql"]

  vpc_security_group_ids = [aws_security_group.rds.id]
  db_subnet_group_name   = aws_db_subnet_group.main.name

  skip_final_snapshot = var.environment != "production"
}

# AWS S3 for Object Storage
resource "aws_s3_bucket" "loco_storage" {
  bucket = "loco-${var.environment}-storage-${random_id.bucket_suffix.hex}"
}

resource "aws_s3_bucket_versioning" "loco_storage" {
  bucket = aws_s3_bucket.loco_storage.id

  versioning_configuration {
    status = "Enabled"
  }
}

# AWS CloudFront for CDN
resource "aws_cloudfront_distribution" "loco_cdn" {
  enabled             = true
  is_ipv6_enabled    = true
  default_root_object = "index.html"

  origin {
    domain_name = aws_s3_bucket.loco_storage.bucket_regional_domain_name
    origin_id   = "S3-${aws_s3_bucket.loco_storage.id}"

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.main.cloudfront_access_identity_path
    }
  }

  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = "S3-${aws_s3_bucket.loco_storage.id}"

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }

    viewer_protocol_policy = "redirect-to-https"
    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
    compress               = true
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }
}

# ==================== Azure Configuration ====================

provider "azurerm" {
  features {}
  subscription_id = var.azure_subscription_id
  skip_provider_registration = true
}

# Azure Resource Group
resource "azurerm_resource_group" "loco" {
  name     = "loco-${var.environment}-rg"
  location = var.azure_region
}

# Azure Functions for Serverless
resource "azurerm_service_plan" "loco_functions" {
  name                = "loco-${var.environment}-asp"
  resource_group_name = azurerm_resource_group.loco.name
  location           = azurerm_resource_group.loco.location
  os_type            = "Linux"
  sku_name           = "Y1"
}

resource "azurerm_linux_function_app" "loco_functions" {
  name                = "loco-${var.environment}-func"
  resource_group_name = azurerm_resource_group.loco.name
  location           = azurerm_resource_group.loco.location

  storage_account_name       = azurerm_storage_account.loco.name
  storage_account_access_key = azurerm_storage_account.loco.primary_access_key
  service_plan_id            = azurerm_service_plan.loco_functions.id

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }

    cors {
      allowed_origins = var.cors_origins
    }
  }

  app_settings = merge(
    var.common_env_vars,
    {
      "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
      "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.loco.instrumentation_key
    }
  )
}

# Azure AKS Cluster
resource "azurerm_kubernetes_cluster" "loco" {
  name                = "loco-${var.environment}-aks"
  location            = azurerm_resource_group.loco.location
  resource_group_name = azurerm_resource_group.loco.name
  dns_prefix          = "loco-${var.environment}"

  default_node_pool {
    name       = "default"
    node_count = 3
    vm_size    = "Standard_D2_v2"

    enable_auto_scaling = true
    min_count          = 2
    max_count          = 10
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin    = "azure"
    load_balancer_sku = "standard"
  }
}

# Azure Cosmos DB for Global Distribution
resource "azurerm_cosmosdb_account" "loco" {
  name                = "loco-${var.environment}-cosmos"
  location            = azurerm_resource_group.loco.location
  resource_group_name = azurerm_resource_group.loco.name
  offer_type          = "Standard"

  enable_automatic_failover = true
  enable_multiple_write_locations = true

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = var.azure_region
    failover_priority = 0
  }

  geo_location {
    location          = var.azure_secondary_region
    failover_priority = 1
  }
}

# Azure Application Insights
resource "azurerm_application_insights" "loco" {
  name                = "loco-${var.environment}-appinsights"
  location            = azurerm_resource_group.loco.location
  resource_group_name = azurerm_resource_group.loco.name
  application_type    = "web"
}

# ==================== Google Cloud Configuration ====================

provider "google" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

# GCP Cloud Run for Serverless Containers
resource "google_cloud_run_service" "loco_api" {
  name     = "loco-${var.environment}-api"
  location = var.gcp_region

  template {
    spec {
      containers {
        image = "gcr.io/${var.gcp_project_id}/loco-api:latest"

        resources {
          limits = {
            cpu    = "2"
            memory = "2Gi"
          }
        }

        env {
          name  = "ENVIRONMENT"
          value = var.environment
        }
      }

      service_account_name = google_service_account.loco_run.email
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/minScale" = "1"
        "autoscaling.knative.dev/maxScale" = "100"
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

# GCP GKE Cluster
resource "google_container_cluster" "loco" {
  name     = "loco-${var.environment}-gke"
  location = var.gcp_region

  initial_node_count = 3

  node_config {
    preemptible  = var.environment != "production"
    machine_type = "e2-medium"

    oauth_scopes = [
      "https://www.googleapis.com/auth/cloud-platform"
    ]
  }

  cluster_autoscaling {
    enabled = true

    resource_limits {
      resource_type = "cpu"
      minimum       = 2
      maximum       = 100
    }

    resource_limits {
      resource_type = "memory"
      minimum       = 4
      maximum       = 200
    }
  }

  workload_identity_config {
    workload_pool = "${var.gcp_project_id}.svc.id.goog"
  }
}

# GCP Cloud SQL for PostgreSQL
resource "google_sql_database_instance" "loco_db" {
  name             = "loco-${var.environment}-postgres"
  database_version = "POSTGRES_15"
  region           = var.gcp_region

  settings {
    tier = "db-f1-micro"

    database_flags {
      name  = "max_connections"
      value = "100"
    }

    backup_configuration {
      enabled                        = true
      start_time                     = "03:00"
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = 7
    }

    insights_config {
      query_insights_enabled  = true
      query_string_length    = 1024
      record_application_tags = true
      record_client_address  = true
    }
  }
}

# GCP Firestore for NoSQL
resource "google_firestore_database" "loco" {
  project     = var.gcp_project_id
  name        = "loco-${var.environment}"
  location_id = var.gcp_region
  type        = "FIRESTORE_NATIVE"
}

# GCP Cloud Storage
resource "google_storage_bucket" "loco_storage" {
  name          = "loco-${var.environment}-storage-${random_id.bucket_suffix.hex}"
  location      = var.gcp_region
  force_destroy = var.environment != "production"

  versioning {
    enabled = true
  }

  lifecycle_rule {
    condition {
      age = 30
    }
    action {
      type = "Delete"
    }
  }
}

# ==================== Global Load Balancing ====================

# Multi-cloud DNS with Cloud DNS (GCP)
resource "google_dns_managed_zone" "loco" {
  name     = "loco-zone"
  dns_name = "${var.domain_name}."
}

resource "google_dns_record_set" "aws_endpoint" {
  name = "aws.${google_dns_managed_zone.loco.dns_name}"
  type = "A"
  ttl  = 300

  managed_zone = google_dns_managed_zone.loco.name
  rrdatas      = [aws_eip.main.public_ip]
}

resource "google_dns_record_set" "azure_endpoint" {
  name = "azure.${google_dns_managed_zone.loco.dns_name}"
  type = "A"
  ttl  = 300

  managed_zone = google_dns_managed_zone.loco.name
  rrdatas      = [azurerm_public_ip.main.ip_address]
}

resource "google_dns_record_set" "gcp_endpoint" {
  name = "gcp.${google_dns_managed_zone.loco.dns_name}"
  type = "A"
  ttl  = 300

  managed_zone = google_dns_managed_zone.loco.name
  rrdatas      = [google_compute_global_address.main.address]
}

# ==================== Monitoring & Observability ====================

# Datadog Integration for Multi-cloud Monitoring
resource "datadog_monitor" "multi_cloud_health" {
  name               = "Loco Multi-Cloud Health"
  type               = "composite"
  message            = "Multi-cloud infrastructure health alert @pagerduty"
  escalation_message = "Multi-cloud infrastructure critical @oncall"

  query = <<-EOT
    ( avg(last_5m):avg:aws.lambda.duration{function:loco-*} > 1000 )
    || ( avg(last_5m):avg:azure.functions.duration{app:loco-*} > 1000 )
    || ( avg(last_5m):avg:gcp.run.request.latencies{service:loco-*} > 1000 )
  EOT

  monitor_thresholds {
    critical = 2000
    warning  = 1000
  }

  tags = ["environment:${var.environment}", "project:loco", "multi-cloud"]
}

# ==================== Outputs ====================

output "aws_api_endpoint" {
  value = aws_apigatewayv2_api.loco_api.api_endpoint
}

output "azure_function_url" {
  value = azurerm_linux_function_app.loco_functions.default_hostname
}

output "gcp_cloud_run_url" {
  value = google_cloud_run_service.loco_api.status[0].url
}

output "eks_cluster_endpoint" {
  value = module.eks.cluster_endpoint
}

output "aks_cluster_endpoint" {
  value = azurerm_kubernetes_cluster.loco.kube_config.0.host
}

output "gke_cluster_endpoint" {
  value = google_container_cluster.loco.endpoint
}