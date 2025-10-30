using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.BPO;

/// <summary>
/// BPO-Specific Automation Templates - Philippines $40B Market
///
/// Research Source (Round 4 - Philippines):
/// - BPO Market Size: $37.38B (2024) → $40B (2025), CAGR 10.6%
/// - AI Adoption: 67% of BPO companies already implementing AI
/// - Investment Growth: 78% plan to increase AI investment by 2025
/// - Global AI in BPO: $3.5B by 2026
/// - Efficiency Gains: 30% operational efficiency increase from automation
/// - Key Services: Customer support, data entry, quality assurance, ticket routing
///
/// Supported BPO Use Cases:
/// 1. Customer Support Automation (Call Center Operations)
/// 2. Data Entry and Processing (Forms, Documents, Spreadsheets)
/// 3. Quality Assurance and Compliance Monitoring
/// 4. Ticket Routing and Escalation Management
/// 5. Email Response Automation
/// 6. Chat Support Bot Integration
/// 7. Claims Processing (Insurance, Healthcare)
/// 8. Invoice Processing and Accounts Payable
/// 9. HR Onboarding and Offboarding
/// 10. Survey and Feedback Processing
///
/// ROI Metrics from Philippines Market:
/// - 30% increase in operational efficiency
/// - 50-80% reduction in processing time
/// - 85-95% accuracy improvement
/// - $10-30 cost savings per transaction
/// </summary>
public class BPOAutomationTemplates
{
    private readonly Dictionary<string, BPOTemplate> _templates = new();
    private readonly Dictionary<string, BPOWorkflow> _workflowInstances = new();

    public BPOAutomationTemplates()
    {
        InitializeBPOTemplates();
    }

    private void InitializeBPOTemplates()
    {
        // 1. Customer Support Automation
        _templates["customer-support-ticket-handling"] = new BPOTemplate
        {
            TemplateId = "customer-support-ticket-handling",
            Name = "Customer Support Ticket Handling",
            Category = BPOCategory.CustomerSupport,
            Description = "Automated ticket classification, routing, and response generation for customer support operations",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 40,
                TimeReductionPercentage = 60,
                CostSavingsPerTransaction = 15m,
                AccuracyPercentage = 92,
                PaybackPeriodDays = 90
            },
            Steps = new List<BPOWorkflowStep>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Ticket Intake",
                    Type = StepType.DataCollection,
                    Description = "Collect ticket from multiple channels (email, chat, phone, web form)",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low,
                    EstimatedTimeManual = TimeSpan.FromMinutes(2),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(5)
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Ticket Classification",
                    Type = StepType.AIAnalysis,
                    Description = "Use NLP to classify ticket by category, urgency, and sentiment",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(3),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(10),
                    RequiredCapabilities = new List<string> { "NLP", "Sentiment Analysis", "Text Classification" }
                },
                new()
                {
                    StepNumber = 3,
                    Name = "Auto-Response Generation",
                    Type = StepType.AIGeneration,
                    Description = "Generate contextual response using AI for common queries",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    EstimatedTimeManual = TimeSpan.FromMinutes(10),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(15),
                    RequiredCapabilities = new List<string> { "LLM", "Knowledge Base", "Response Templates" }
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Routing Decision",
                    Type = StepType.Decision,
                    Description = "Route to agent if complex, auto-close if resolved, escalate if urgent",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(1),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(2)
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Quality Check",
                    Type = StepType.Validation,
                    Description = "Validate response quality and compliance before sending",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(2),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(5)
                }
            },
            Metrics = new List<BPOMetric>
            {
                new() { Name = "Average Handle Time (AHT)", Unit = "minutes", TargetValue = 5, BaselineValue = 18 },
                new() { Name = "First Contact Resolution (FCR)", Unit = "percentage", TargetValue = 85, BaselineValue = 65 },
                new() { Name = "Customer Satisfaction (CSAT)", Unit = "score", TargetValue = 4.5, BaselineValue = 3.8 }
            }
        };

        // 2. Data Entry Automation
        _templates["data-entry-invoice-processing"] = new BPOTemplate
        {
            TemplateId = "data-entry-invoice-processing",
            Name = "Invoice Data Entry and Processing",
            Category = BPOCategory.DataEntry,
            Description = "Automated extraction, validation, and entry of invoice data into accounting systems",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 75,
                TimeReductionPercentage = 85,
                CostSavingsPerTransaction = 25m,
                AccuracyPercentage = 98,
                PaybackPeriodDays = 60
            },
            Steps = new List<BPOWorkflowStep>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Document Capture",
                    Type = StepType.DataCollection,
                    Description = "Receive invoice via email, scan, or upload",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low,
                    EstimatedTimeManual = TimeSpan.FromMinutes(1),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(5)
                },
                new()
                {
                    StepNumber = 2,
                    Name = "OCR Extraction",
                    Type = StepType.DataExtraction,
                    Description = "Extract invoice fields using OCR and intelligent document processing",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(5),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(15),
                    RequiredCapabilities = new List<string> { "OCR", "IDP", "Table Extraction" }
                },
                new()
                {
                    StepNumber = 3,
                    Name = "Data Validation",
                    Type = StepType.Validation,
                    Description = "Validate extracted data against business rules and PO matching",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(3),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(10)
                },
                new()
                {
                    StepNumber = 4,
                    Name = "ERP Entry",
                    Type = StepType.SystemIntegration,
                    Description = "Enter validated data into ERP/accounting system",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    EstimatedTimeManual = TimeSpan.FromMinutes(8),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(20),
                    RequiredCapabilities = new List<string> { "API Integration", "RPA", "Database Access" }
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Exception Handling",
                    Type = StepType.HumanReview,
                    Description = "Route exceptions to human reviewer for resolution",
                    IsAutomatable = false,
                    AutomationComplexity = AutomationComplexity.NotApplicable,
                    EstimatedTimeManual = TimeSpan.FromMinutes(10),
                    EstimatedTimeAutomated = TimeSpan.FromMinutes(10)
                }
            },
            Metrics = new List<BPOMetric>
            {
                new() { Name = "Processing Time per Invoice", Unit = "minutes", TargetValue = 2, BaselineValue = 17 },
                new() { Name = "Accuracy Rate", Unit = "percentage", TargetValue = 98, BaselineValue = 92 },
                new() { Name = "Touchless Processing Rate", Unit = "percentage", TargetValue = 80, BaselineValue = 15 }
            }
        };

        // 3. Quality Assurance Automation
        _templates["qa-call-monitoring"] = new BPOTemplate
        {
            TemplateId = "qa-call-monitoring",
            Name = "Automated Call Quality Monitoring",
            Category = BPOCategory.QualityAssurance,
            Description = "AI-powered analysis of customer service calls for quality, compliance, and training",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 90,
                TimeReductionPercentage = 95,
                CostSavingsPerTransaction = 12m,
                AccuracyPercentage = 88,
                PaybackPeriodDays = 120
            },
            Steps = new List<BPOWorkflowStep>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Call Recording Ingestion",
                    Type = StepType.DataCollection,
                    Description = "Automatically ingest call recordings from telephony system",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low,
                    EstimatedTimeManual = TimeSpan.FromMinutes(1),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(5)
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Speech-to-Text Transcription",
                    Type = StepType.DataExtraction,
                    Description = "Convert audio to text using ASR (Automatic Speech Recognition)",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(20),
                    EstimatedTimeAutomated = TimeSpan.FromMinutes(2),
                    RequiredCapabilities = new List<string> { "ASR", "Speaker Diarization", "Multi-Language" }
                },
                new()
                {
                    StepNumber = 3,
                    Name = "Quality Scoring",
                    Type = StepType.AIAnalysis,
                    Description = "Analyze call for adherence to script, tone, compliance, resolution",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    EstimatedTimeManual = TimeSpan.FromMinutes(30),
                    EstimatedTimeAutomated = TimeSpan.FromMinutes(1),
                    RequiredCapabilities = new List<string> { "NLP", "Sentiment Analysis", "Compliance Rules" }
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Coaching Insights Generation",
                    Type = StepType.AIGeneration,
                    Description = "Generate personalized coaching recommendations for agent",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    EstimatedTimeManual = TimeSpan.FromMinutes(15),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(30),
                    RequiredCapabilities = new List<string> { "LLM", "Performance Analytics" }
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Compliance Check",
                    Type = StepType.Validation,
                    Description = "Flag regulatory compliance issues (PCI-DSS, HIPAA, etc.)",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    EstimatedTimeManual = TimeSpan.FromMinutes(10),
                    EstimatedTimeAutomated = TimeSpan.FromSeconds(15)
                }
            },
            Metrics = new List<BPOMetric>
            {
                new() { Name = "Calls Monitored per Month", Unit = "count", TargetValue = 5000, BaselineValue = 500 },
                new() { Name = "QA Analyst Productivity", Unit = "calls/day", TargetValue = 200, BaselineValue = 20 },
                new() { Name = "Compliance Detection Rate", Unit = "percentage", TargetValue = 95, BaselineValue = 70 }
            }
        };

        // 4. Email Response Automation
        _templates["email-auto-response"] = new BPOTemplate
        {
            TemplateId = "email-auto-response",
            Name = "Automated Email Classification and Response",
            Category = BPOCategory.CustomerSupport,
            Description = "AI-powered email triage, classification, and automated response generation",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 65,
                TimeReductionPercentage = 80,
                CostSavingsPerTransaction = 8m,
                AccuracyPercentage = 90,
                PaybackPeriodDays = 75
            },
            Steps = new List<BPOWorkflowStep>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Email Ingestion",
                    Type = StepType.DataCollection,
                    Description = "Monitor inbox and ingest emails automatically",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Intent Classification",
                    Type = StepType.AIAnalysis,
                    Description = "Classify email intent (inquiry, complaint, request, etc.)",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium,
                    RequiredCapabilities = new List<string> { "NLP", "Intent Recognition" }
                },
                new()
                {
                    StepNumber = 3,
                    Name = "Response Generation",
                    Type = StepType.AIGeneration,
                    Description = "Generate contextual response using knowledge base and LLM",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    RequiredCapabilities = new List<string> { "LLM", "RAG", "Template Engine" }
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Human-in-the-Loop Review",
                    Type = StepType.HumanReview,
                    Description = "Optional review before sending (based on confidence score)",
                    IsAutomatable = false,
                    AutomationComplexity = AutomationComplexity.NotApplicable
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Response Dispatch",
                    Type = StepType.SystemIntegration,
                    Description = "Send email response and log interaction in CRM",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low
                }
            }
        };

        // 5. Claims Processing (Insurance/Healthcare BPO)
        _templates["insurance-claims-processing"] = new BPOTemplate
        {
            TemplateId = "insurance-claims-processing",
            Name = "Insurance Claims Processing Automation",
            Category = BPOCategory.ClaimsProcessing,
            Description = "End-to-end automation of insurance claims from intake to adjudication",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 70,
                TimeReductionPercentage = 75,
                CostSavingsPerTransaction = 35m,
                AccuracyPercentage = 96,
                PaybackPeriodDays = 90
            },
            Steps = new List<BPOWorkflowStep>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Claim Intake",
                    Type = StepType.DataCollection,
                    Description = "Receive claim via portal, email, or fax",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Low
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Document Extraction",
                    Type = StepType.DataExtraction,
                    Description = "Extract claim details from forms, medical records, receipts",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    RequiredCapabilities = new List<string> { "OCR", "IDP", "Medical Coding AI" }
                },
                new()
                {
                    StepNumber = 3,
                    Name = "Policy Verification",
                    Type = StepType.Validation,
                    Description = "Verify coverage, eligibility, and policy limits",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Fraud Detection",
                    Type = StepType.AIAnalysis,
                    Description = "AI-powered fraud risk scoring",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High,
                    RequiredCapabilities = new List<string> { "ML Models", "Anomaly Detection", "Pattern Recognition" }
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Adjudication",
                    Type = StepType.Decision,
                    Description = "Auto-approve, deny, or route to adjuster based on rules and AI",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.High
                },
                new()
                {
                    StepNumber = 6,
                    Name = "Payment Processing",
                    Type = StepType.SystemIntegration,
                    Description = "Process payment if approved",
                    IsAutomatable = true,
                    AutomationComplexity = AutomationComplexity.Medium
                }
            },
            Metrics = new List<BPOMetric>
            {
                new() { Name = "Straight-Through Processing (STP) Rate", Unit = "percentage", TargetValue = 70, BaselineValue = 20 },
                new() { Name = "Average Processing Time", Unit = "days", TargetValue = 2, BaselineValue = 8 },
                new() { Name = "Fraud Detection Rate", Unit = "percentage", TargetValue = 92, BaselineValue = 75 }
            }
        };

        // 6. HR Onboarding Automation
        _templates["hr-employee-onboarding"] = new BPOTemplate
        {
            TemplateId = "hr-employee-onboarding",
            Name = "Employee Onboarding Automation",
            Category = BPOCategory.HRProcessing,
            Description = "Streamline new hire onboarding with automated document collection, system provisioning, and training",
            EstimatedROI = new ROIMetrics
            {
                EfficiencyGainPercentage = 50,
                TimeReductionPercentage = 70,
                CostSavingsPerTransaction = 150m,
                AccuracyPercentage = 95,
                PaybackPeriodDays = 60
            },
            Steps = new List<BPOWorkflowStep>
            {
                new() { StepNumber = 1, Name = "Offer Acceptance", Type = StepType.DataCollection },
                new() { StepNumber = 2, Name = "Document Collection", Type = StepType.DataCollection },
                new() { StepNumber = 3, Name = "Background Check Initiation", Type = StepType.SystemIntegration },
                new() { StepNumber = 4, Name = "IT Provisioning", Type = StepType.SystemIntegration },
                new() { StepNumber = 5, Name = "Training Assignment", Type = StepType.SystemIntegration },
                new() { StepNumber = 6, Name = "Welcome Communications", Type = StepType.Communication }
            }
        };
    }

    /// <summary>
    /// Get BPO template by ID
    /// </summary>
    public BPOTemplate? GetTemplate(string templateId)
    {
        return _templates.TryGetValue(templateId, out var template) ? template : null;
    }

    /// <summary>
    /// Get all templates for a specific BPO category
    /// </summary>
    public List<BPOTemplate> GetTemplatesByCategory(BPOCategory category)
    {
        return _templates.Values.Where(t => t.Category == category).ToList();
    }

    /// <summary>
    /// Instantiate a workflow from a template
    /// </summary>
    public async Task<BPOWorkflow> CreateWorkflowFromTemplateAsync(
        string templateId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new ArgumentException($"Template not found: {templateId}");
        }

        await Task.Delay(50, cancellationToken);

        var workflow = new BPOWorkflow
        {
            WorkflowId = Guid.NewGuid().ToString(),
            TemplateId = templateId,
            Name = template.Name,
            Category = template.Category,
            Status = BPOWorkflowStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Parameters = parameters,
            Steps = template.Steps.Select(s => new BPOWorkflowStepInstance
            {
                StepNumber = s.StepNumber,
                Name = s.Name,
                Type = s.Type,
                Status = StepStatus.Pending,
                EstimatedTime = s.EstimatedTimeAutomated
            }).ToList()
        };

        _workflowInstances[workflow.WorkflowId] = workflow;
        return workflow;
    }

    /// <summary>
    /// Execute a BPO workflow instance
    /// </summary>
    public async Task<BPOWorkflowExecutionResult> ExecuteWorkflowAsync(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        if (!_workflowInstances.TryGetValue(workflowId, out var workflow))
        {
            throw new ArgumentException($"Workflow not found: {workflowId}");
        }

        var result = new BPOWorkflowExecutionResult
        {
            WorkflowId = workflowId,
            StartTime = DateTime.UtcNow
        };

        workflow.Status = BPOWorkflowStatus.Running;
        workflow.StartedAt = DateTime.UtcNow;

        try
        {
            foreach (var step in workflow.Steps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                step.Status = StepStatus.Running;
                step.StartedAt = DateTime.UtcNow;

                // Simulate step execution
                await Task.Delay((int)step.EstimatedTime.TotalMilliseconds, cancellationToken);

                step.Status = StepStatus.Completed;
                step.CompletedAt = DateTime.UtcNow;
                step.ActualTime = step.CompletedAt.Value - step.StartedAt.Value;

                result.CompletedSteps++;
            }

            workflow.Status = BPOWorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
            result.Status = BPOWorkflowStatus.Completed;
        }
        catch (Exception ex)
        {
            workflow.Status = BPOWorkflowStatus.Failed;
            result.Status = BPOWorkflowStatus.Failed;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime.Value - result.StartTime;
        }

        return result;
    }

    /// <summary>
    /// Calculate ROI for a template based on volume
    /// </summary>
    public ROICalculation CalculateROI(string templateId, int monthlyVolume, decimal laborCostPerHour)
    {
        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new ArgumentException($"Template not found: {templateId}");
        }

        var manualTimePerTransaction = template.Steps.Sum(s => s.EstimatedTimeManual.TotalHours);
        var automatedTimePerTransaction = template.Steps.Sum(s => s.EstimatedTimeAutomated.TotalHours);
        var timeSavingsPerTransaction = manualTimePerTransaction - automatedTimePerTransaction;

        var monthlyCostManual = (decimal)(monthlyVolume * manualTimePerTransaction) * laborCostPerHour;
        var monthlyCostAutomated = (decimal)(monthlyVolume * automatedTimePerTransaction) * laborCostPerHour;
        var monthlySavings = monthlyCostManual - monthlyCostAutomated;
        var annualSavings = monthlySavings * 12;

        return new ROICalculation
        {
            TemplateId = templateId,
            TemplateName = template.Name,
            MonthlyVolume = monthlyVolume,
            LaborCostPerHour = laborCostPerHour,
            TimeSavingsPerTransaction = TimeSpan.FromHours(timeSavingsPerTransaction),
            MonthlyTimeSavingsHours = timeSavingsPerTransaction * monthlyVolume,
            MonthlyCostSavings = monthlySavings,
            AnnualCostSavings = annualSavings,
            EfficiencyGainPercentage = template.EstimatedROI.EfficiencyGainPercentage,
            PaybackPeriodDays = template.EstimatedROI.PaybackPeriodDays
        };
    }
}

// Supporting types

public enum BPOCategory
{
    CustomerSupport,
    DataEntry,
    QualityAssurance,
    ClaimsProcessing,
    HRProcessing,
    Finance,
    BackOffice,
    FrontOffice
}

public enum StepType
{
    DataCollection,
    DataExtraction,
    DataValidation,
    AIAnalysis,
    AIGeneration,
    Decision,
    SystemIntegration,
    HumanReview,
    Communication,
    Validation
}

public enum AutomationComplexity
{
    Low,
    Medium,
    High,
    VeryHigh,
    NotApplicable
}

public enum BPOWorkflowStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Paused
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public class BPOTemplate
{
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BPOCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<BPOWorkflowStep> Steps { get; set; } = new();
    public ROIMetrics EstimatedROI { get; set; } = new();
    public List<BPOMetric> Metrics { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class BPOWorkflowStep
{
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public StepType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsAutomatable { get; set; }
    public AutomationComplexity AutomationComplexity { get; set; }
    public TimeSpan EstimatedTimeManual { get; set; }
    public TimeSpan EstimatedTimeAutomated { get; set; }
    public List<string> RequiredCapabilities { get; set; } = new();
}

public class ROIMetrics
{
    public double EfficiencyGainPercentage { get; set; }
    public double TimeReductionPercentage { get; set; }
    public decimal CostSavingsPerTransaction { get; set; }
    public double AccuracyPercentage { get; set; }
    public int PaybackPeriodDays { get; set; }
}

public class BPOMetric
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double BaselineValue { get; set; }
}

public class BPOWorkflow
{
    public string WorkflowId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BPOCategory Category { get; set; }
    public BPOWorkflowStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<BPOWorkflowStepInstance> Steps { get; set; } = new();
}

public class BPOWorkflowStepInstance
{
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public StepType Type { get; set; }
    public StepStatus Status { get; set; }
    public TimeSpan EstimatedTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan ActualTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BPOWorkflowExecutionResult
{
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public BPOWorkflowStatus Status { get; set; }
    public int CompletedSteps { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ROICalculation
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int MonthlyVolume { get; set; }
    public decimal LaborCostPerHour { get; set; }
    public TimeSpan TimeSavingsPerTransaction { get; set; }
    public double MonthlyTimeSavingsHours { get; set; }
    public decimal MonthlyCostSavings { get; set; }
    public decimal AnnualCostSavings { get; set; }
    public double EfficiencyGainPercentage { get; set; }
    public int PaybackPeriodDays { get; set; }
}
