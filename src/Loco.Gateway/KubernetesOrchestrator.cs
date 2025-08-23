using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using YamlDotNet.Serialization;

namespace Loco.Gateway;

/// <summary>
/// Kubernetes orchestration service for cloud-native deployment
/// </summary>
public class KubernetesOrchestrator
{
    private readonly ILogger<KubernetesOrchestrator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKubernetes _kubernetesClient;
    private readonly string _namespace;

    public KubernetesOrchestrator(
        ILogger<KubernetesOrchestrator> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _namespace = configuration["Kubernetes:Namespace"] ?? "loco-system";
        
        var config = KubernetesClientConfiguration.IsInCluster() 
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile();
            
        _kubernetesClient = new Kubernetes(config);
    }

    /// <summary>
    /// Deploy Loco microservices to Kubernetes
    /// </summary>
    public async Task<DeploymentResult> DeployAsync(DeploymentSpec spec)
    {
        var result = new DeploymentResult();
        
        try
        {
            // Create namespace if not exists
            await EnsureNamespaceAsync();
            
            // Deploy ConfigMaps
            foreach (var configMap in spec.ConfigMaps)
            {
                await DeployConfigMapAsync(configMap);
                result.DeployedResources.Add($"ConfigMap/{configMap.Name}");
            }
            
            // Deploy Secrets
            foreach (var secret in spec.Secrets)
            {
                await DeploySecretAsync(secret);
                result.DeployedResources.Add($"Secret/{secret.Name}");
            }
            
            // Deploy Services
            foreach (var service in spec.Services)
            {
                await DeployServiceAsync(service);
                result.DeployedResources.Add($"Service/{service.Name}");
            }
            
            // Deploy Deployments
            foreach (var deployment in spec.Deployments)
            {
                await DeployDeploymentAsync(deployment);
                result.DeployedResources.Add($"Deployment/{deployment.Name}");
            }
            
            // Deploy Ingress
            if (spec.Ingress != null)
            {
                await DeployIngressAsync(spec.Ingress);
                result.DeployedResources.Add($"Ingress/{spec.Ingress.Name}");
            }
            
            // Deploy HPA (Horizontal Pod Autoscaler)
            foreach (var hpa in spec.AutoScalers)
            {
                await DeployHPAAsync(hpa);
                result.DeployedResources.Add($"HPA/{hpa.Name}");
            }
            
            result.Success = true;
            result.Message = "Deployment completed successfully";
            
            _logger.LogInformation("Kubernetes deployment successful: {Resources}", 
                string.Join(", ", result.DeployedResources));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Deployment failed: {ex.Message}";
            result.Errors.Add(ex.ToString());
            
            _logger.LogError(ex, "Kubernetes deployment failed");
        }
        
        return result;
    }

    /// <summary>
    /// Scale deployment replicas
    /// </summary>
    public async Task<bool> ScaleAsync(string deploymentName, int replicas)
    {
        try
        {
            var deployment = await _kubernetesClient.ReadNamespacedDeploymentAsync(
                deploymentName, _namespace);
            
            deployment.Spec.Replicas = replicas;
            
            await _kubernetesClient.ReplaceNamespacedDeploymentAsync(
                deployment, deploymentName, _namespace);
            
            _logger.LogInformation("Scaled {Deployment} to {Replicas} replicas", 
                deploymentName, replicas);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scale deployment {Deployment}", deploymentName);
            return false;
        }
    }

    /// <summary>
    /// Get deployment status
    /// </summary>
    public async Task<DeploymentStatus> GetStatusAsync(string deploymentName)
    {
        try
        {
            var deployment = await _kubernetesClient.ReadNamespacedDeploymentStatusAsync(
                deploymentName, _namespace);
            
            return new DeploymentStatus
            {
                Name = deploymentName,
                Replicas = deployment.Status.Replicas ?? 0,
                ReadyReplicas = deployment.Status.ReadyReplicas ?? 0,
                UpdatedReplicas = deployment.Status.UpdatedReplicas ?? 0,
                AvailableReplicas = deployment.Status.AvailableReplicas ?? 0,
                Conditions = deployment.Status.Conditions?.Select(c => new DeploymentCondition
                {
                    Type = c.Type,
                    Status = c.Status,
                    Reason = c.Reason,
                    Message = c.Message
                }).ToList() ?? new List<DeploymentCondition>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for deployment {Deployment}", deploymentName);
            throw;
        }
    }

    /// <summary>
    /// Perform rolling update
    /// </summary>
    public async Task<bool> RollingUpdateAsync(string deploymentName, string newImage)
    {
        try
        {
            var deployment = await _kubernetesClient.ReadNamespacedDeploymentAsync(
                deploymentName, _namespace);
            
            // Update container image
            foreach (var container in deployment.Spec.Template.Spec.Containers)
            {
                if (container.Name == deploymentName)
                {
                    container.Image = newImage;
                }
            }
            
            // Update deployment
            await _kubernetesClient.ReplaceNamespacedDeploymentAsync(
                deployment, deploymentName, _namespace);
            
            // Wait for rollout to complete
            await WaitForRolloutAsync(deploymentName);
            
            _logger.LogInformation("Rolling update completed for {Deployment} with image {Image}", 
                deploymentName, newImage);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rolling update failed for deployment {Deployment}", deploymentName);
            return false;
        }
    }

    /// <summary>
    /// Generate Kubernetes manifests
    /// </summary>
    public string GenerateManifests(ApplicationSpec appSpec)
    {
        var manifests = new StringBuilder();
        var serializer = new SerializerBuilder().Build();
        
        // Generate Namespace
        var ns = new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = _namespace
            }
        };
        manifests.AppendLine("---");
        manifests.AppendLine(serializer.Serialize(ns));
        
        // Generate Deployment
        var deployment = new V1Deployment
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = new V1ObjectMeta
            {
                Name = appSpec.Name,
                NamespaceProperty = _namespace,
                Labels = new Dictionary<string, string>
                {
                    ["app"] = appSpec.Name,
                    ["version"] = appSpec.Version
                }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = appSpec.Replicas,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        ["app"] = appSpec.Name
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new Dictionary<string, string>
                        {
                            ["app"] = appSpec.Name,
                            ["version"] = appSpec.Version
                        }
                    },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = appSpec.Name,
                                Image = appSpec.Image,
                                Ports = appSpec.Ports.Select(p => new V1ContainerPort
                                {
                                    ContainerPort = p,
                                    Protocol = "TCP"
                                }).ToList(),
                                Resources = new V1ResourceRequirements
                                {
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["cpu"] = new ResourceQuantity(appSpec.CpuLimit),
                                        ["memory"] = new ResourceQuantity(appSpec.MemoryLimit)
                                    },
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["cpu"] = new ResourceQuantity(appSpec.CpuRequest),
                                        ["memory"] = new ResourceQuantity(appSpec.MemoryRequest)
                                    }
                                },
                                LivenessProbe = new V1Probe
                                {
                                    HttpGet = new V1HTTPGetAction
                                    {
                                        Path = "/health",
                                        Port = appSpec.Ports.First()
                                    },
                                    InitialDelaySeconds = 30,
                                    PeriodSeconds = 10
                                },
                                ReadinessProbe = new V1Probe
                                {
                                    HttpGet = new V1HTTPGetAction
                                    {
                                        Path = "/ready",
                                        Port = appSpec.Ports.First()
                                    },
                                    InitialDelaySeconds = 5,
                                    PeriodSeconds = 5
                                }
                            }
                        }
                    }
                }
            }
        };
        
        manifests.AppendLine("---");
        manifests.AppendLine(serializer.Serialize(deployment));
        
        // Generate Service
        var service = new V1Service
        {
            ApiVersion = "v1",
            Kind = "Service",
            Metadata = new V1ObjectMeta
            {
                Name = appSpec.Name,
                NamespaceProperty = _namespace
            },
            Spec = new V1ServiceSpec
            {
                Selector = new Dictionary<string, string>
                {
                    ["app"] = appSpec.Name
                },
                Ports = appSpec.Ports.Select(p => new V1ServicePort
                {
                    Port = p,
                    TargetPort = p,
                    Protocol = "TCP"
                }).ToList(),
                Type = appSpec.ExposeExternal ? "LoadBalancer" : "ClusterIP"
            }
        };
        
        manifests.AppendLine("---");
        manifests.AppendLine(serializer.Serialize(service));
        
        // Generate HPA
        if (appSpec.AutoScale)
        {
            var hpa = new V2HorizontalPodAutoscaler
            {
                ApiVersion = "autoscaling/v2",
                Kind = "HorizontalPodAutoscaler",
                Metadata = new V1ObjectMeta
                {
                    Name = $"{appSpec.Name}-hpa",
                    NamespaceProperty = _namespace
                },
                Spec = new V2HorizontalPodAutoscalerSpec
                {
                    ScaleTargetRef = new V2CrossVersionObjectReference
                    {
                        ApiVersion = "apps/v1",
                        Kind = "Deployment",
                        Name = appSpec.Name
                    },
                    MinReplicas = appSpec.MinReplicas,
                    MaxReplicas = appSpec.MaxReplicas,
                    Metrics = new List<V2MetricSpec>
                    {
                        new V2MetricSpec
                        {
                            Type = "Resource",
                            Resource = new V2ResourceMetricSource
                            {
                                Name = "cpu",
                                Target = new V2MetricTarget
                                {
                                    Type = "Utilization",
                                    AverageUtilization = appSpec.TargetCPUUtilization
                                }
                            }
                        }
                    }
                }
            };
            
            manifests.AppendLine("---");
            manifests.AppendLine(serializer.Serialize(hpa));
        }
        
        return manifests.ToString();
    }

    private async Task EnsureNamespaceAsync()
    {
        try
        {
            await _kubernetesClient.ReadNamespaceAsync(_namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var ns = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = _namespace
                }
            };
            
            await _kubernetesClient.CreateNamespaceAsync(ns);
            _logger.LogInformation("Created namespace: {Namespace}", _namespace);
        }
    }

    private async Task DeployConfigMapAsync(ConfigMapSpec spec)
    {
        var configMap = new V1ConfigMap
        {
            Metadata = new V1ObjectMeta
            {
                Name = spec.Name,
                NamespaceProperty = _namespace
            },
            Data = spec.Data
        };
        
        try
        {
            await _kubernetesClient.CreateNamespacedConfigMapAsync(configMap, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await _kubernetesClient.ReplaceNamespacedConfigMapAsync(configMap, spec.Name, _namespace);
        }
    }

    private async Task DeploySecretAsync(SecretSpec spec)
    {
        var secret = new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = spec.Name,
                NamespaceProperty = _namespace
            },
            Type = "Opaque",
            Data = spec.Data.ToDictionary(
                kvp => kvp.Key,
                kvp => Encoding.UTF8.GetBytes(kvp.Value))
        };
        
        try
        {
            await _kubernetesClient.CreateNamespacedSecretAsync(secret, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await _kubernetesClient.ReplaceNamespacedSecretAsync(secret, spec.Name, _namespace);
        }
    }

    private async Task DeployServiceAsync(ServiceSpec spec)
    {
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = spec.Name,
                NamespaceProperty = _namespace
            },
            Spec = new V1ServiceSpec
            {
                Selector = spec.Selector,
                Ports = spec.Ports.Select(p => new V1ServicePort
                {
                    Name = p.Name,
                    Port = p.Port,
                    TargetPort = p.TargetPort,
                    Protocol = p.Protocol
                }).ToList(),
                Type = spec.Type
            }
        };
        
        try
        {
            await _kubernetesClient.CreateNamespacedServiceAsync(service, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await _kubernetesClient.ReplaceNamespacedServiceAsync(service, spec.Name, _namespace);
        }
    }

    private async Task DeployDeploymentAsync(DeploymentSpec spec)
    {
        var deployment = CreateDeployment(spec);
        
        try
        {
            await _kubernetesClient.CreateNamespacedDeploymentAsync(deployment, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await _kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, spec.Name, _namespace);
        }
    }

    private V1Deployment CreateDeployment(DeploymentSpec spec)
    {
        return new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = spec.Name,
                NamespaceProperty = _namespace,
                Labels = spec.Labels
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = spec.Replicas,
                Selector = new V1LabelSelector
                {
                    MatchLabels = spec.Selector
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = spec.Selector
                    },
                    Spec = new V1PodSpec
                    {
                        Containers = spec.Containers.Select(c => new V1Container
                        {
                            Name = c.Name,
                            Image = c.Image,
                            Ports = c.Ports?.Select(p => new V1ContainerPort
                            {
                                ContainerPort = p
                            }).ToList(),
                            Env = c.Environment?.Select(e => new V1EnvVar
                            {
                                Name = e.Key,
                                Value = e.Value
                            }).ToList(),
                            Resources = new V1ResourceRequirements
                            {
                                Limits = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new ResourceQuantity(c.CpuLimit ?? "1"),
                                    ["memory"] = new ResourceQuantity(c.MemoryLimit ?? "1Gi")
                                },
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new ResourceQuantity(c.CpuRequest ?? "100m"),
                                    ["memory"] = new ResourceQuantity(c.MemoryRequest ?? "128Mi")
                                }
                            }
                        }).ToList()
                    }
                }
            }
        };
    }

    private async Task DeployIngressAsync(IngressSpec spec)
    {
        // Simplified ingress deployment
        _logger.LogInformation("Deploying ingress: {Name}", spec.Name);
        await Task.CompletedTask;
    }

    private async Task DeployHPAAsync(AutoScalerSpec spec)
    {
        var hpa = new V2HorizontalPodAutoscaler
        {
            Metadata = new V1ObjectMeta
            {
                Name = spec.Name,
                NamespaceProperty = _namespace
            },
            Spec = new V2HorizontalPodAutoscalerSpec
            {
                ScaleTargetRef = new V2CrossVersionObjectReference
                {
                    ApiVersion = "apps/v1",
                    Kind = "Deployment",
                    Name = spec.TargetDeployment
                },
                MinReplicas = spec.MinReplicas,
                MaxReplicas = spec.MaxReplicas,
                Metrics = new List<V2MetricSpec>
                {
                    new V2MetricSpec
                    {
                        Type = "Resource",
                        Resource = new V2ResourceMetricSource
                        {
                            Name = "cpu",
                            Target = new V2MetricTarget
                            {
                                Type = "Utilization",
                                AverageUtilization = spec.TargetCPUUtilization
                            }
                        }
                    }
                }
            }
        };
        
        try
        {
            await _kubernetesClient.CreateNamespacedHorizontalPodAutoscalerAsync(hpa, _namespace);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await _kubernetesClient.ReplaceNamespacedHorizontalPodAutoscalerAsync(hpa, spec.Name, _namespace);
        }
    }

    private async Task WaitForRolloutAsync(string deploymentName, int timeoutSeconds = 300)
    {
        var startTime = DateTime.UtcNow;
        
        while ((DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds)
        {
            var status = await GetStatusAsync(deploymentName);
            
            if (status.ReadyReplicas == status.Replicas && 
                status.UpdatedReplicas == status.Replicas)
            {
                return;
            }
            
            await Task.Delay(5000);
        }
        
        throw new TimeoutException($"Rollout did not complete within {timeoutSeconds} seconds");
    }
}

// Supporting classes for Kubernetes orchestration
public class DeploymentSpec
{
    public string Name { get; set; } = string.Empty;
    public int Replicas { get; set; } = 1;
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Selector { get; set; } = new();
    public List<ContainerSpec> Containers { get; set; } = new();
    public List<ConfigMapSpec> ConfigMaps { get; set; } = new();
    public List<SecretSpec> Secrets { get; set; } = new();
    public List<ServiceSpec> Services { get; set; } = new();
    public List<DeploymentSpec> Deployments { get; set; } = new();
    public IngressSpec? Ingress { get; set; }
    public List<AutoScalerSpec> AutoScalers { get; set; } = new();
}

public class ContainerSpec
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<int> Ports { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();
    public string? CpuLimit { get; set; }
    public string? MemoryLimit { get; set; }
    public string? CpuRequest { get; set; }
    public string? MemoryRequest { get; set; }
}

public class ConfigMapSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();
}

public class SecretSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();
}

public class ServiceSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Selector { get; set; } = new();
    public List<PortSpec> Ports { get; set; } = new();
    public string Type { get; set; } = "ClusterIP";
}

public class PortSpec
{
    public string Name { get; set; } = string.Empty;
    public int Port { get; set; }
    public int TargetPort { get; set; }
    public string Protocol { get; set; } = "TCP";
}

public class IngressSpec
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public List<IngressPath> Paths { get; set; } = new();
}

public class IngressPath
{
    public string Path { get; set; } = "/";
    public string ServiceName { get; set; } = string.Empty;
    public int ServicePort { get; set; }
}

public class AutoScalerSpec
{
    public string Name { get; set; } = string.Empty;
    public string TargetDeployment { get; set; } = string.Empty;
    public int MinReplicas { get; set; } = 1;
    public int MaxReplicas { get; set; } = 10;
    public int TargetCPUUtilization { get; set; } = 80;
}

public class ApplicationSpec
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Image { get; set; } = string.Empty;
    public int Replicas { get; set; } = 3;
    public List<int> Ports { get; set; } = new();
    public bool ExposeExternal { get; set; }
    public bool AutoScale { get; set; }
    public int MinReplicas { get; set; } = 1;
    public int MaxReplicas { get; set; } = 10;
    public int TargetCPUUtilization { get; set; } = 80;
    public string CpuLimit { get; set; } = "1";
    public string MemoryLimit { get; set; } = "1Gi";
    public string CpuRequest { get; set; } = "100m";
    public string MemoryRequest { get; set; } = "128Mi";
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> DeployedResources { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class DeploymentStatus
{
    public string Name { get; set; } = string.Empty;
    public int Replicas { get; set; }
    public int ReadyReplicas { get; set; }
    public int UpdatedReplicas { get; set; }
    public int AvailableReplicas { get; set; }
    public List<DeploymentCondition> Conditions { get; set; } = new();
}

public class DeploymentCondition
{
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Message { get; set; }
}
