using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Services
{
    /// <summary>
    /// Advanced search service for automation rules
    /// Implements full-text search, filters, and intelligent ranking
    /// </summary>
    public sealed class RuleSearchService
    {
        private readonly ILogger<RuleSearchService> _logger;
        private readonly SearchIndexer _indexer;
        private readonly List<ISearchFilter> _filters;
        private readonly List<ISearchRanker> _rankers;

        public RuleSearchService(ILogger<RuleSearchService> logger = null)
        {
            _logger = logger;
            _indexer = new SearchIndexer();
            _filters = new List<ISearchFilter>();
            _rankers = new List<ISearchRanker>();
            
            InitializeDefaultFilters();
            InitializeDefaultRankers();
        }

        private void InitializeDefaultFilters()
        {
            _filters.Add(new TypeFilter());
            _filters.Add(new StatusFilter());
            _filters.Add(new DateRangeFilter());
            _filters.Add(new TagFilter());
            _filters.Add(new TriggerTypeFilter());
            _filters.Add(new ActionTypeFilter());
        }

        private void InitializeDefaultRankers()
        {
            _rankers.Add(new RelevanceRanker());
            _rankers.Add(new RecencyRanker());
            _rankers.Add(new FrequencyRanker());
            _rankers.Add(new ComplexityRanker());
        }

        /// <summary>
        /// Search rules with query and filters
        /// </summary>
        public async Task<SearchResult> SearchRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            SearchQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var rulesList = rules?.ToList() ?? new List<AutomationDsl.Rule>();
                
                // Build index if needed
                if (query.UseIndexing)
                {
                    await _indexer.BuildIndexAsync(rulesList);
                }

                // Start with all rules
                IEnumerable<SearchItem> searchItems = rulesList.Select(r => new SearchItem
                {
                    Rule = r,
                    Score = 0,
                    Matches = new List<SearchMatch>()
                });

                // Apply text search
                if (!string.IsNullOrWhiteSpace(query.SearchText))
                {
                    searchItems = await ApplyTextSearchAsync(searchItems, query.SearchText, query.SearchOptions);
                }

                // Apply filters
                foreach (var filter in query.Filters)
                {
                    var filterImpl = _filters.FirstOrDefault(f => f.Name == filter.Type);
                    if (filterImpl != null)
                    {
                        searchItems = await filterImpl.ApplyAsync(searchItems, filter);
                    }
                }

                // Apply custom filter predicate
                if (query.CustomFilter != null)
                {
                    searchItems = searchItems.Where(item => query.CustomFilter(item.Rule));
                }

                // Calculate scores and rank
                var rankedItems = await RankResultsAsync(searchItems.ToList(), query);

                // Apply sorting
                rankedItems = ApplySorting(rankedItems, query.SortBy, query.SortDescending);

                // Apply pagination
                var totalCount = rankedItems.Count;
                var pagedItems = rankedItems
                    .Skip(query.Skip)
                    .Take(query.Take)
                    .ToList();

                sw.Stop();

                _logger?.LogInformation("Search completed in {ElapsedMs}ms, found {Count} results",
                    sw.ElapsedMilliseconds, totalCount);

                return new SearchResult
                {
                    Success = true,
                    Items = pagedItems,
                    TotalCount = totalCount,
                    Query = query,
                    SearchTime = sw.Elapsed,
                    Facets = query.IncludeFacets ? await BuildFacetsAsync(rankedItems) : null
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Search failed");
                sw.Stop();
                
                return new SearchResult
                {
                    Success = false,
                    Error = ex.Message,
                    SearchTime = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Quick search with simple text query
        /// </summary>
        public async Task<List<AutomationDsl.Rule>> QuickSearchAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            string searchText,
            int maxResults = 10)
        {
            var query = new SearchQuery
            {
                SearchText = searchText,
                Take = maxResults,
                SearchOptions = SearchOptions.Default
            };

            var result = await SearchRulesAsync(rules, query);
            return result.Success 
                ? result.Items.Select(i => i.Rule).ToList()
                : new List<AutomationDsl.Rule>();
        }

        /// <summary>
        /// Search suggestions (autocomplete)
        /// </summary>
        public async Task<List<SearchSuggestion>> GetSuggestionsAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            string prefix,
            int maxSuggestions = 5)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return new List<SearchSuggestion>();

            var suggestions = new List<SearchSuggestion>();
            var rulesList = rules?.ToList() ?? new List<AutomationDsl.Rule>();
            
            // Search in rule names
            var nameMatches = rulesList
                .Where(r => r.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                .Select(r => new SearchSuggestion
                {
                    Text = r.Name,
                    Type = "Rule Name",
                    Category = "Rules"
                })
                .Take(maxSuggestions);
            
            suggestions.AddRange(nameMatches);

            // Search in trigger types
            var triggerTypes = rulesList
                .Where(r => r.Trigger != null)
                .Select(r => r.Trigger.Type)
                .Distinct()
                .Where(t => t?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                .Select(t => new SearchSuggestion
                {
                    Text = t,
                    Type = "Trigger Type",
                    Category = "Triggers"
                })
                .Take(maxSuggestions - suggestions.Count);
            
            suggestions.AddRange(triggerTypes);

            return suggestions.Take(maxSuggestions).ToList();
        }

        /// <summary>
        /// Find similar rules
        /// </summary>
        public async Task<List<AutomationDsl.Rule>> FindSimilarRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            AutomationDsl.Rule targetRule,
            int maxResults = 5)
        {
            if (targetRule == null)
                return new List<AutomationDsl.Rule>();

            var rulesList = rules?.Where(r => r.Id != targetRule.Id).ToList() 
                ?? new List<AutomationDsl.Rule>();

            var similarities = new List<(AutomationDsl.Rule Rule, double Score)>();

            foreach (var rule in rulesList)
            {
                var score = CalculateSimilarity(targetRule, rule);
                if (score > 0.3) // Minimum similarity threshold
                {
                    similarities.Add((rule, score));
                }
            }

            return similarities
                .OrderByDescending(s => s.Score)
                .Take(maxResults)
                .Select(s => s.Rule)
                .ToList();
        }

        // Private methods
        private async Task<IEnumerable<SearchItem>> ApplyTextSearchAsync(
            IEnumerable<SearchItem> items,
            string searchText,
            SearchOptions options)
        {
            var results = new List<SearchItem>();
            var searchTerms = ParseSearchTerms(searchText, options);

            foreach (var item in items)
            {
                var matches = new List<SearchMatch>();
                var score = 0.0;

                // Search in name
                if (options.SearchInNames)
                {
                    var nameMatches = FindMatches(item.Rule.Name, searchTerms, "Name", 2.0);
                    matches.AddRange(nameMatches);
                    score += nameMatches.Sum(m => m.Score);
                }

                // Search in description
                if (options.SearchInDescriptions && !string.IsNullOrEmpty(item.Rule.Description))
                {
                    var descMatches = FindMatches(item.Rule.Description, searchTerms, "Description", 1.0);
                    matches.AddRange(descMatches);
                    score += descMatches.Sum(m => m.Score);
                }

                // Search in trigger parameters
                if (options.SearchInParameters && item.Rule.Trigger?.Parameters != null)
                {
                    foreach (var param in item.Rule.Trigger.Parameters)
                    {
                        var paramMatches = FindMatches(param.Value?.ToString(), searchTerms, 
                            $"Trigger.{param.Key}", 0.8);
                        matches.AddRange(paramMatches);
                        score += paramMatches.Sum(m => m.Score);
                    }
                }

                // Search in action parameters
                if (options.SearchInParameters && item.Rule.Actions != null)
                {
                    foreach (var action in item.Rule.Actions)
                    {
                        if (action.Parameters != null)
                        {
                            foreach (var param in action.Parameters)
                            {
                                var paramMatches = FindMatches(param.Value?.ToString(), searchTerms,
                                    $"Action.{param.Key}", 0.8);
                                matches.AddRange(paramMatches);
                                score += paramMatches.Sum(m => m.Score);
                            }
                        }
                    }
                }

                if (matches.Any())
                {
                    item.Matches = matches;
                    item.Score = score;
                    results.Add(item);
                }
            }

            return results;
        }

        private List<string> ParseSearchTerms(string searchText, SearchOptions options)
        {
            var terms = new List<string>();

            if (options.UseRegex)
            {
                terms.Add(searchText);
            }
            else
            {
                // Parse quoted strings
                var quotedPattern = @"""([^""]+)""";
                var quotedMatches = Regex.Matches(searchText, quotedPattern);
                
                foreach (Match match in quotedMatches)
                {
                    terms.Add(match.Groups[1].Value);
                }

                // Remove quoted parts and split remaining
                var remaining = Regex.Replace(searchText, quotedPattern, "");
                var words = remaining.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                terms.AddRange(words);
            }

            return terms.Distinct().ToList();
        }

        private List<SearchMatch> FindMatches(string text, List<string> searchTerms, string field, double weight)
        {
            var matches = new List<SearchMatch>();
            
            if (string.IsNullOrEmpty(text))
                return matches;

            foreach (var term in searchTerms)
            {
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(new SearchMatch
                    {
                        Field = field,
                        Term = term,
                        Score = weight,
                        Position = text.IndexOf(term, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            return matches;
        }

        private async Task<List<SearchItem>> RankResultsAsync(List<SearchItem> items, SearchQuery query)
        {
            foreach (var ranker in _rankers)
            {
                if (query.RankingWeights.TryGetValue(ranker.Name, out var weight) && weight > 0)
                {
                    await ranker.RankAsync(items, weight);
                }
            }

            // Sort by final score
            return items.OrderByDescending(i => i.Score).ToList();
        }

        private List<SearchItem> ApplySorting(List<SearchItem> items, string sortBy, bool descending)
        {
            if (string.IsNullOrEmpty(sortBy))
                return items;

            return sortBy.ToLower() switch
            {
                "name" => descending 
                    ? items.OrderByDescending(i => i.Rule.Name).ToList()
                    : items.OrderBy(i => i.Rule.Name).ToList(),
                "score" => descending
                    ? items.OrderByDescending(i => i.Score).ToList()
                    : items.OrderBy(i => i.Score).ToList(),
                "created" => descending
                    ? items.OrderByDescending(i => i.Rule.CreatedAt).ToList()
                    : items.OrderBy(i => i.Rule.CreatedAt).ToList(),
                "modified" => descending
                    ? items.OrderByDescending(i => i.Rule.ModifiedAt).ToList()
                    : items.OrderBy(i => i.Rule.ModifiedAt).ToList(),
                _ => items
            };
        }

        private async Task<SearchFacets> BuildFacetsAsync(List<SearchItem> items)
        {
            return await Task.Run(() =>
            {
                var facets = new SearchFacets();

                // Trigger type facet
                facets.TriggerTypes = items
                    .Where(i => i.Rule.Trigger != null)
                    .GroupBy(i => i.Rule.Trigger.Type)
                    .Select(g => new FacetValue
                    {
                        Value = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(f => f.Count)
                    .ToList();

                // Action type facet
                facets.ActionTypes = items
                    .SelectMany(i => i.Rule.Actions ?? new List<AutomationDsl.ActionDefinition>())
                    .GroupBy(a => a.Type)
                    .Select(g => new FacetValue
                    {
                        Value = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(f => f.Count)
                    .ToList();

                // Status facet
                facets.Statuses = items
                    .GroupBy(i => i.Rule.IsEnabled)
                    .Select(g => new FacetValue
                    {
                        Value = g.Key ? "Enabled" : "Disabled",
                        Count = g.Count()
                    })
                    .ToList();

                return facets;
            });
        }

        private double CalculateSimilarity(AutomationDsl.Rule rule1, AutomationDsl.Rule rule2)
        {
            var score = 0.0;
            var factors = 0;

            // Name similarity
            if (!string.IsNullOrEmpty(rule1.Name) && !string.IsNullOrEmpty(rule2.Name))
            {
                score += CalculateStringSimilarity(rule1.Name, rule2.Name);
                factors++;
            }

            // Trigger type similarity
            if (rule1.Trigger?.Type == rule2.Trigger?.Type)
            {
                score += 1.0;
                factors++;
            }

            // Action types similarity
            var actions1 = rule1.Actions?.Select(a => a.Type).ToHashSet() ?? new HashSet<string>();
            var actions2 = rule2.Actions?.Select(a => a.Type).ToHashSet() ?? new HashSet<string>();
            if (actions1.Any() && actions2.Any())
            {
                var intersection = actions1.Intersect(actions2).Count();
                var union = actions1.Union(actions2).Count();
                score += (double)intersection / union;
                factors++;
            }

            return factors > 0 ? score / factors : 0;
        }

        private double CalculateStringSimilarity(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            // Simple Levenshtein distance-based similarity
            var distance = LevenshteinDistance(s1.ToLower(), s2.ToLower());
            var maxLength = Math.Max(s1.Length, s2.Length);
            return 1.0 - ((double)distance / maxLength);
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var m = s1.Length;
            var n = s2.Length;
            var d = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++)
                d[i, 0] = i;
            for (int j = 0; j <= n; j++)
                d[0, j] = j;

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(
                        d[i - 1, j] + 1,
                        d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[m, n];
        }
    }

    // Supporting classes
    public class SearchQuery
    {
        public string SearchText { get; set; }
        public SearchOptions SearchOptions { get; set; } = SearchOptions.Default;
        public List<SearchFilter> Filters { get; set; } = new List<SearchFilter>();
        public Func<AutomationDsl.Rule, bool> CustomFilter { get; set; }
        public Dictionary<string, double> RankingWeights { get; set; } = new Dictionary<string, double>
        {
            ["Relevance"] = 1.0,
            ["Recency"] = 0.5,
            ["Frequency"] = 0.3,
            ["Complexity"] = 0.2
        };
        public string SortBy { get; set; } = "score";
        public bool SortDescending { get; set; } = true;
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public bool UseIndexing { get; set; } = false;
        public bool IncludeFacets { get; set; } = false;
    }

    public class SearchOptions
    {
        public bool SearchInNames { get; set; } = true;
        public bool SearchInDescriptions { get; set; } = true;
        public bool SearchInParameters { get; set; } = true;
        public bool SearchInTags { get; set; } = true;
        public bool CaseSensitive { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public bool WholeWord { get; set; } = false;

        public static SearchOptions Default => new SearchOptions();
        public static SearchOptions NameOnly => new SearchOptions 
        { 
            SearchInDescriptions = false,
            SearchInParameters = false,
            SearchInTags = false
        };
    }

    public class SearchFilter
    {
        public string Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class SearchResult
    {
        public bool Success { get; set; }
        public List<SearchItem> Items { get; set; }
        public int TotalCount { get; set; }
        public SearchQuery Query { get; set; }
        public TimeSpan SearchTime { get; set; }
        public SearchFacets Facets { get; set; }
        public string Error { get; set; }
    }

    public class SearchItem
    {
        public AutomationDsl.Rule Rule { get; set; }
        public double Score { get; set; }
        public List<SearchMatch> Matches { get; set; }
    }

    public class SearchMatch
    {
        public string Field { get; set; }
        public string Term { get; set; }
        public double Score { get; set; }
        public int Position { get; set; }
    }

    public class SearchSuggestion
    {
        public string Text { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public double Score { get; set; }
    }

    public class SearchFacets
    {
        public List<FacetValue> TriggerTypes { get; set; }
        public List<FacetValue> ActionTypes { get; set; }
        public List<FacetValue> Statuses { get; set; }
        public List<FacetValue> Tags { get; set; }
    }

    public class FacetValue
    {
        public string Value { get; set; }
        public int Count { get; set; }
    }

    // Search components
    public interface ISearchFilter
    {
        string Name { get; }
        Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter);
    }

    public interface ISearchRanker
    {
        string Name { get; }
        Task RankAsync(List<SearchItem> items, double weight);
    }

    public class SearchIndexer
    {
        private Dictionary<string, List<int>> _index;

        public async Task BuildIndexAsync(IEnumerable<AutomationDsl.Rule> rules)
        {
            await Task.Run(() =>
            {
                _index = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                var rulesList = rules.ToList();

                for (int i = 0; i < rulesList.Count; i++)
                {
                    var rule = rulesList[i];
                    IndexText(rule.Name, i);
                    IndexText(rule.Description, i);
                }
            });
        }

        private void IndexText(string text, int ruleIndex)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (!_index.TryGetValue(word, out var indices))
                {
                    indices = new List<int>();
                    _index[word] = indices;
                }
                if (!indices.Contains(ruleIndex))
                {
                    indices.Add(ruleIndex);
                }
            }
        }
    }

    // Filter implementations
    public class TypeFilter : ISearchFilter
    {
        public string Name => "Type";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            if (!filter.Parameters.TryGetValue("types", out var typesObj))
                return Task.FromResult(items);

            var types = typesObj as List<string> ?? new List<string>();
            if (!types.Any())
                return Task.FromResult(items);

            var filtered = items.Where(i => 
                types.Contains(i.Rule.Trigger?.Type, StringComparer.OrdinalIgnoreCase));

            return Task.FromResult(filtered);
        }
    }

    public class StatusFilter : ISearchFilter
    {
        public string Name => "Status";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            if (!filter.Parameters.TryGetValue("enabled", out var enabledObj))
                return Task.FromResult(items);

            if (enabledObj is bool enabled)
            {
                var filtered = items.Where(i => i.Rule.IsEnabled == enabled);
                return Task.FromResult(filtered);
            }

            return Task.FromResult(items);
        }
    }

    public class DateRangeFilter : ISearchFilter
    {
        public string Name => "DateRange";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            DateTime? from = null;
            DateTime? to = null;

            if (filter.Parameters.TryGetValue("from", out var fromObj) && fromObj is DateTime fromDate)
                from = fromDate;

            if (filter.Parameters.TryGetValue("to", out var toObj) && toObj is DateTime toDate)
                to = toDate;

            var filtered = items.Where(i =>
            {
                var date = i.Rule.ModifiedAt ?? i.Rule.CreatedAt ?? DateTime.MinValue;
                return (!from.HasValue || date >= from.Value) &&
                       (!to.HasValue || date <= to.Value);
            });

            return Task.FromResult(filtered);
        }
    }

    public class TagFilter : ISearchFilter
    {
        public string Name => "Tags";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            if (!filter.Parameters.TryGetValue("tags", out var tagsObj))
                return Task.FromResult(items);

            var tags = tagsObj as List<string> ?? new List<string>();
            if (!tags.Any())
                return Task.FromResult(items);

            var filtered = items.Where(i => 
                i.Rule.Tags?.Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)) == true);

            return Task.FromResult(filtered);
        }
    }

    public class TriggerTypeFilter : ISearchFilter
    {
        public string Name => "TriggerType";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            if (!filter.Parameters.TryGetValue("types", out var typesObj))
                return Task.FromResult(items);

            var types = typesObj as List<string> ?? new List<string>();
            if (!types.Any())
                return Task.FromResult(items);

            var filtered = items.Where(i =>
                types.Contains(i.Rule.Trigger?.Type, StringComparer.OrdinalIgnoreCase));

            return Task.FromResult(filtered);
        }
    }

    public class ActionTypeFilter : ISearchFilter
    {
        public string Name => "ActionType";

        public Task<IEnumerable<SearchItem>> ApplyAsync(IEnumerable<SearchItem> items, SearchFilter filter)
        {
            if (!filter.Parameters.TryGetValue("types", out var typesObj))
                return Task.FromResult(items);

            var types = typesObj as List<string> ?? new List<string>();
            if (!types.Any())
                return Task.FromResult(items);

            var filtered = items.Where(i =>
                i.Rule.Actions?.Any(a => types.Contains(a.Type, StringComparer.OrdinalIgnoreCase)) == true);

            return Task.FromResult(filtered);
        }
    }

    // Ranker implementations
    public class RelevanceRanker : ISearchRanker
    {
        public string Name => "Relevance";

        public Task RankAsync(List<SearchItem> items, double weight)
        {
            // Score is already calculated during text search
            foreach (var item in items)
            {
                item.Score *= weight;
            }
            return Task.CompletedTask;
        }
    }

    public class RecencyRanker : ISearchRanker
    {
        public string Name => "Recency";

        public Task RankAsync(List<SearchItem> items, double weight)
        {
            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                var date = item.Rule.ModifiedAt ?? item.Rule.CreatedAt ?? DateTime.MinValue;
                var daysSince = (now - date).TotalDays;
                var recencyScore = Math.Max(0, 1.0 - (daysSince / 365.0)); // Decay over a year
                item.Score += recencyScore * weight;
            }
            return Task.CompletedTask;
        }
    }

    public class FrequencyRanker : ISearchRanker
    {
        public string Name => "Frequency";

        public Task RankAsync(List<SearchItem> items, double weight)
        {
            foreach (var item in items)
            {
                // Score based on execution frequency (if tracked)
                var frequency = item.Rule.ExecutionCount ?? 0;
                var frequencyScore = Math.Min(1.0, frequency / 100.0); // Cap at 100 executions
                item.Score += frequencyScore * weight;
            }
            return Task.CompletedTask;
        }
    }

    public class ComplexityRanker : ISearchRanker
    {
        public string Name => "Complexity";

        public Task RankAsync(List<SearchItem> items, double weight)
        {
            foreach (var item in items)
            {
                // Score based on rule complexity
                var complexity = 0;
                complexity += item.Rule.Actions?.Count ?? 0;
                complexity += item.Rule.Conditions?.Count ?? 0;
                complexity += item.Rule.Trigger?.Parameters?.Count ?? 0;
                
                var complexityScore = Math.Min(1.0, complexity / 10.0); // Cap at 10 components
                item.Score += complexityScore * weight;
            }
            return Task.CompletedTask;
        }
    }
}
