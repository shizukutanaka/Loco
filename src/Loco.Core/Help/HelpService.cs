using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Help
{
    /// <summary>
    /// Contextual help and tooltip system
    /// Simple and effective, following Rob Pike's philosophy
    /// </summary>
    public interface IHelpService
    {
        Task<HelpContent> GetHelpAsync(string topic);
        Task<Tooltip> GetTooltipAsync(string componentId);
        Task<List<HelpTopic>> SearchHelpAsync(string query);
        Task<List<HelpTopic>> GetAllTopicsAsync();
        Task<bool> RegisterHelpContentAsync(HelpContent content);
        Task<bool> RegisterTooltipAsync(Tooltip tooltip);
        Task<GuidedTour> GetGuidedTourAsync(string tourId);
        Task<List<GuidedTour>> GetAvailableToursAsync();
    }

    public class HelpService : IHelpService
    {
        private readonly ILogger<HelpService> _logger;
        private readonly Dictionary<string, HelpContent> _helpContents;
        private readonly Dictionary<string, Tooltip> _tooltips;
        private readonly Dictionary<string, GuidedTour> _tours;
        private readonly ILocalizationService _localization;

        public HelpService(ILogger<HelpService> logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
            _helpContents = new Dictionary<string, HelpContent>(StringComparer.OrdinalIgnoreCase);
            _tooltips = new Dictionary<string, Tooltip>(StringComparer.OrdinalIgnoreCase);
            _tours = new Dictionary<string, GuidedTour>(StringComparer.OrdinalIgnoreCase);
            
            InitializeDefaultContent();
        }

        public async Task<HelpContent> GetHelpAsync(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return null;
            }

            // Try exact match first
            if (_helpContents.TryGetValue(topic, out var content))
            {
                return await LocalizeContentAsync(content);
            }

            // Try fuzzy match
            var fuzzyMatch = _helpContents.Keys
                .FirstOrDefault(k => k.Contains(topic, StringComparison.OrdinalIgnoreCase));
            
            if (fuzzyMatch != null && _helpContents.TryGetValue(fuzzyMatch, out content))
            {
                return await LocalizeContentAsync(content);
            }

            _logger.LogWarning("Help topic not found: {Topic}", topic);
            return null;
        }

        public async Task<Tooltip> GetTooltipAsync(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
            {
                return null;
            }

            if (_tooltips.TryGetValue(componentId, out var tooltip))
            {
                return await LocalizeTooltipAsync(tooltip);
            }

            // Try parent component
            var lastDot = componentId.LastIndexOf('.');
            if (lastDot > 0)
            {
                var parentId = componentId.Substring(0, lastDot);
                return await GetTooltipAsync(parentId);
            }

            return null;
        }

        public async Task<List<HelpTopic>> SearchHelpAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return new List<HelpTopic>();
            }

            var results = new List<HelpTopic>();
            var searchTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var content in _helpContents.Values)
            {
                var score = CalculateRelevanceScore(content, searchTerms);
                if (score > 0)
                {
                    results.Add(new HelpTopic
                    {
                        Id = content.Id,
                        Title = content.Title,
                        Category = content.Category,
                        RelevanceScore = score
                    });
                }
            }

            return await Task.FromResult(
                results.OrderByDescending(r => r.RelevanceScore)
                       .Take(10)
                       .ToList());
        }

        public async Task<List<HelpTopic>> GetAllTopicsAsync()
        {
            var topics = _helpContents.Values
                .Select(c => new HelpTopic
                {
                    Id = c.Id,
                    Title = c.Title,
                    Category = c.Category,
                    Tags = c.Tags
                })
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Title)
                .ToList();

            return await Task.FromResult(topics);
        }

        public async Task<bool> RegisterHelpContentAsync(HelpContent content)
        {
            if (content == null || string.IsNullOrEmpty(content.Id))
            {
                return false;
            }

            _helpContents[content.Id] = content;
            _logger.LogDebug("Registered help content: {Id}", content.Id);
            
            return await Task.FromResult(true);
        }

        public async Task<bool> RegisterTooltipAsync(Tooltip tooltip)
        {
            if (tooltip == null || string.IsNullOrEmpty(tooltip.ComponentId))
            {
                return false;
            }

            _tooltips[tooltip.ComponentId] = tooltip;
            _logger.LogDebug("Registered tooltip for: {ComponentId}", tooltip.ComponentId);
            
            return await Task.FromResult(true);
        }

        public async Task<GuidedTour> GetGuidedTourAsync(string tourId)
        {
            if (string.IsNullOrEmpty(tourId))
            {
                return null;
            }

            if (_tours.TryGetValue(tourId, out var tour))
            {
                return await LocalizeTourAsync(tour);
            }

            return null;
        }

        public async Task<List<GuidedTour>> GetAvailableToursAsync()
        {
            var tours = _tours.Values
                .OrderBy(t => t.Order)
                .ToList();

            var localizedTours = new List<GuidedTour>();
            foreach (var tour in tours)
            {
                localizedTours.Add(await LocalizeTourAsync(tour));
            }

            return localizedTours;
        }

        private void InitializeDefaultContent()
        {
            // Core help topics
            RegisterHelpContentAsync(new HelpContent
            {
                Id = "getting-started",
                Title = "Getting Started",
                Category = "Basics",
                Content = "Welcome to Loco! This guide will help you get started with automation.",
                Tags = new[] { "start", "begin", "new", "introduction" },
                Examples = new[]
                {
                    new HelpExample
                    {
                        Title = "Create your first rule",
                        Code = "loco build",
                        Description = "Launch the interactive flow builder"
                    }
                },
                RelatedTopics = new[] { "rules", "flows", "automation" }
            });

            RegisterHelpContentAsync(new HelpContent
            {
                Id = "rules",
                Title = "Automation Rules",
                Category = "Core Concepts",
                Content = "Rules are the basic building blocks of automation in Loco.",
                Tags = new[] { "rule", "automation", "trigger", "action" },
                Examples = new[]
                {
                    new HelpExample
                    {
                        Title = "Time-based rule",
                        Code = @"{
  ""trigger"": { ""type"": ""time.schedule"", ""config"": { ""hour"": 9 } },
  ""actions"": [{ ""type"": ""notification.show"" }]
}",
                        Description = "Trigger a notification at 9 AM"
                    }
                }
            });

            // Component tooltips
            RegisterTooltipAsync(new Tooltip
            {
                ComponentId = "time.schedule",
                Title = "Schedule Trigger",
                Description = "Triggers at specified times",
                ShortcutKey = null,
                MoreInfoLink = "help://triggers/time-schedule"
            });

            RegisterTooltipAsync(new Tooltip
            {
                ComponentId = "file.copy",
                Title = "Copy Files",
                Description = "Copies files from source to destination",
                ShortcutKey = null,
                Parameters = new[]
                {
                    new TooltipParameter
                    {
                        Name = "source",
                        Type = "string",
                        Description = "Source file or directory path",
                        Required = true
                    },
                    new TooltipParameter
                    {
                        Name = "destination",
                        Type = "string",
                        Description = "Destination path",
                        Required = true
                    }
                }
            });

            RegisterTooltipAsync(new Tooltip
            {
                ComponentId = "notification.show",
                Title = "Show Notification",
                Description = "Displays a system notification",
                Parameters = new[]
                {
                    new TooltipParameter
                    {
                        Name = "title",
                        Type = "string",
                        Description = "Notification title",
                        Required = true
                    },
                    new TooltipParameter
                    {
                        Name = "message",
                        Type = "string",
                        Description = "Notification message",
                        Required = false
                    }
                }
            });

            // Guided tours
            _tours["first-time"] = new GuidedTour
            {
                Id = "first-time",
                Name = "First Time Setup",
                Description = "Learn the basics of Loco",
                Order = 1,
                Steps = new[]
                {
                    new TourStep
                    {
                        TargetElement = "#main-menu",
                        Title = "Main Menu",
                        Content = "Access all features from the main menu",
                        Position = "bottom"
                    },
                    new TourStep
                    {
                        TargetElement = "#create-rule-btn",
                        Title = "Create Rules",
                        Content = "Click here to create your first automation rule",
                        Position = "right"
                    },
                    new TourStep
                    {
                        TargetElement = "#dashboard",
                        Title = "Dashboard",
                        Content = "Monitor all your automations from the dashboard",
                        Position = "center"
                    }
                }
            };

            _tours["advanced-features"] = new GuidedTour
            {
                Id = "advanced-features",
                Name = "Advanced Features",
                Description = "Explore advanced automation capabilities",
                Order = 2,
                Steps = new[]
                {
                    new TourStep
                    {
                        TargetElement = "#flow-composer",
                        Title = "Flow Composer",
                        Content = "Create complex workflows with multiple steps",
                        Position = "right"
                    },
                    new TourStep
                    {
                        TargetElement = "#natural-language",
                        Title = "Natural Language",
                        Content = "Convert plain English to automation rules",
                        Position = "bottom"
                    }
                }
            };
        }

        private int CalculateRelevanceScore(HelpContent content, string[] searchTerms)
        {
            var score = 0;
            var contentLower = $"{content.Title} {content.Content} {string.Join(" ", content.Tags ?? Array.Empty<string>())}".ToLower();

            foreach (var term in searchTerms)
            {
                if (content.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score += 10;
                
                if (content.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score += 8;
                
                if (content.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                    score += 5;
                
                if (content.Tags?.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)) == true)
                    score += 6;
                
                if (contentLower.Contains(term))
                    score += 3;
            }

            return score;
        }

        private async Task<HelpContent> LocalizeContentAsync(HelpContent content)
        {
            if (content == null)
                return null;

            var localized = content.Clone();
            localized.Title = await _localization.GetStringAsync($"help.{content.Id}.title", content.Title);
            localized.Content = await _localization.GetStringAsync($"help.{content.Id}.content", content.Content);
            
            return localized;
        }

        private async Task<Tooltip> LocalizeTooltipAsync(Tooltip tooltip)
        {
            if (tooltip == null)
                return null;

            var localized = tooltip.Clone();
            localized.Title = await _localization.GetStringAsync($"tooltip.{tooltip.ComponentId}.title", tooltip.Title);
            localized.Description = await _localization.GetStringAsync($"tooltip.{tooltip.ComponentId}.description", tooltip.Description);
            
            return localized;
        }

        private async Task<GuidedTour> LocalizeTourAsync(GuidedTour tour)
        {
            if (tour == null)
                return null;

            var localized = tour.Clone();
            localized.Name = await _localization.GetStringAsync($"tour.{tour.Id}.name", tour.Name);
            localized.Description = await _localization.GetStringAsync($"tour.{tour.Id}.description", tour.Description);
            
            for (int i = 0; i < localized.Steps.Length; i++)
            {
                var step = localized.Steps[i];
                step.Title = await _localization.GetStringAsync($"tour.{tour.Id}.step{i}.title", step.Title);
                step.Content = await _localization.GetStringAsync($"tour.{tour.Id}.step{i}.content", step.Content);
            }
            
            return localized;
        }
    }

    public class HelpContent
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
        public string[] Tags { get; set; }
        public HelpExample[] Examples { get; set; }
        public string[] RelatedTopics { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public HelpContent Clone()
        {
            return new HelpContent
            {
                Id = Id,
                Title = Title,
                Category = Category,
                Content = Content,
                Tags = Tags?.ToArray(),
                Examples = Examples?.Select(e => e.Clone()).ToArray(),
                RelatedTopics = RelatedTopics?.ToArray(),
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }
    }

    public class HelpExample
    {
        public string Title { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Language { get; set; } = "json";

        public HelpExample Clone()
        {
            return new HelpExample
            {
                Title = Title,
                Code = Code,
                Description = Description,
                Language = Language
            };
        }
    }

    public class HelpTopic
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string[] Tags { get; set; }
        public int RelevanceScore { get; set; }
    }

    public class Tooltip
    {
        public string ComponentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ShortcutKey { get; set; }
        public TooltipParameter[] Parameters { get; set; }
        public string MoreInfoLink { get; set; }
        public TooltipPosition Position { get; set; } = TooltipPosition.Auto;
        public int ShowDelay { get; set; } = 500;
        public int HideDelay { get; set; } = 100;

        public Tooltip Clone()
        {
            return new Tooltip
            {
                ComponentId = ComponentId,
                Title = Title,
                Description = Description,
                ShortcutKey = ShortcutKey,
                Parameters = Parameters?.Select(p => p.Clone()).ToArray(),
                MoreInfoLink = MoreInfoLink,
                Position = Position,
                ShowDelay = ShowDelay,
                HideDelay = HideDelay
            };
        }
    }

    public class TooltipParameter
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public object DefaultValue { get; set; }

        public TooltipParameter Clone()
        {
            return new TooltipParameter
            {
                Name = Name,
                Type = Type,
                Description = Description,
                Required = Required,
                DefaultValue = DefaultValue
            };
        }
    }

    public enum TooltipPosition
    {
        Auto,
        Top,
        Bottom,
        Left,
        Right,
        Center
    }

    public class GuidedTour
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public TourStep[] Steps { get; set; }
        public bool ShowOnFirstVisit { get; set; }
        public string[] RequiredFeatures { get; set; }

        public GuidedTour Clone()
        {
            return new GuidedTour
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Order = Order,
                Steps = Steps?.Select(s => s.Clone()).ToArray(),
                ShowOnFirstVisit = ShowOnFirstVisit,
                RequiredFeatures = RequiredFeatures?.ToArray()
            };
        }
    }

    public class TourStep
    {
        public string TargetElement { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Position { get; set; }
        public bool HighlightTarget { get; set; } = true;
        public bool AllowSkip { get; set; } = true;
        public string NextButtonText { get; set; } = "Next";
        public string BackButtonText { get; set; } = "Back";

        public TourStep Clone()
        {
            return new TourStep
            {
                TargetElement = TargetElement,
                Title = Title,
                Content = Content,
                Position = Position,
                HighlightTarget = HighlightTarget,
                AllowSkip = AllowSkip,
                NextButtonText = NextButtonText,
                BackButtonText = BackButtonText
            };
        }
    }
}
