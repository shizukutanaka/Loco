using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Core.Storage;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Storage;

/// <summary>
/// Tests for JsonFileRuleStore - JSON file-based rule persistence
/// </summary>
public class JsonFileRuleStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly JsonFileRuleStore _store;

    public JsonFileRuleStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"loco-tests-{Guid.NewGuid()}");
        _testFilePath = Path.Combine(_testDirectory, "rules.json");
        _store = new JsonFileRuleStore(_testFilePath);
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        // Arrange & Act
        var testDir = Path.Combine(Path.GetTempPath(), $"loco-init-{Guid.NewGuid()}");
        var testFile = Path.Combine(testDir, "rules.json");

        // Act
        var store = new JsonFileRuleStore(testFile);

        // Assert
        Directory.Exists(testDir).Should().BeTrue();
        File.Exists(testFile).Should().BeTrue();

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void Constructor_ThrowsOnNullFilePath()
    {
        // Act & Assert
        var action = () => new JsonFileRuleStore(null!);
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CRUD Operations Tests

    [Fact]
    public async Task UpsertRuleAsync_WithNewRule_SavesRule()
    {
        // Arrange
        var rule = new SimpleRule
        {
            Id = "rule-1",
            Name = "Test Rule",
            IsEnabled = true
        };

        // Act
        await _store.UpsertRuleAsync(rule);

        // Assert
        var retrieved = await _store.GetRuleAsync("rule-1");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("rule-1");
        retrieved.Name.Should().Be("Test Rule");
    }

    [Fact]
    public async Task UpsertRuleAsync_WithExistingRule_UpdatesRule()
    {
        // Arrange
        var rule = new SimpleRule { Id = "rule-1", Name = "Original Name" };
        await _store.UpsertRuleAsync(rule);

        var updatedRule = new SimpleRule { Id = "rule-1", Name = "Updated Name" };

        // Act
        await _store.UpsertRuleAsync(updatedRule);

        // Assert
        var retrieved = await _store.GetRuleAsync("rule-1");
        retrieved!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task GetRuleAsync_WithValidId_ReturnsRule()
    {
        // Arrange
        var rule = new SimpleRule { Id = "test-rule", Name = "Test" };
        await _store.UpsertRuleAsync(rule);

        // Act
        var retrieved = await _store.GetRuleAsync("test-rule");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test-rule");
    }

    [Fact]
    public async Task GetRuleAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var retrieved = await _store.GetRuleAsync("non-existent");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRuleAsync_WithValidId_DeletesRule()
    {
        // Arrange
        var rule = new SimpleRule { Id = "delete-me", Name = "Delete Test" };
        await _store.UpsertRuleAsync(rule);

        // Act
        await _store.DeleteRuleAsync("delete-me");

        // Assert
        var retrieved = await _store.GetRuleAsync("delete-me");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task RuleExistsAsync_WithExistingRule_ReturnsTrue()
    {
        // Arrange
        var rule = new SimpleRule { Id = "exists", Name = "Exists Test" };
        await _store.UpsertRuleAsync(rule);

        // Act
        var exists = await _store.RuleExistsAsync("exists");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RuleExistsAsync_WithNonExistentRule_ReturnsFalse()
    {
        // Act
        var exists = await _store.RuleExistsAsync("does-not-exist");

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public async Task GetRulesAsync_ReturnsAllRules()
    {
        // Arrange
        var rule1 = new SimpleRule { Id = "rule-1", Name = "Rule 1" };
        var rule2 = new SimpleRule { Id = "rule-2", Name = "Rule 2" };
        var rule3 = new SimpleRule { Id = "rule-3", Name = "Rule 3" };

        await _store.UpsertRuleAsync(rule1);
        await _store.UpsertRuleAsync(rule2);
        await _store.UpsertRuleAsync(rule3);

        // Act
        var rules = await _store.GetRulesAsync();

        // Assert
        rules.Should().HaveCount(3);
        rules.Should().Contain(r => r.Id == "rule-1");
        rules.Should().Contain(r => r.Id == "rule-2");
        rules.Should().Contain(r => r.Id == "rule-3");
    }

    [Fact]
    public async Task ClearRulesAsync_RemovesAllRules()
    {
        // Arrange
        await _store.UpsertRuleAsync(new SimpleRule { Id = "rule-1" });
        await _store.UpsertRuleAsync(new SimpleRule { Id = "rule-2" });

        // Act
        await _store.ClearRulesAsync();

        // Assert
        var rules = await _store.GetRulesAsync();
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEnabledRulesAsync_ReturnsOnlyEnabledRules()
    {
        // Arrange
        await _store.UpsertRuleAsync(new SimpleRule { Id = "enabled-1", IsEnabled = true });
        await _store.UpsertRuleAsync(new SimpleRule { Id = "disabled-1", IsEnabled = false });
        await _store.UpsertRuleAsync(new SimpleRule { Id = "enabled-2", IsEnabled = true });

        // Act
        var enabledRules = await _store.GetEnabledRulesAsync();

        // Assert
        enabledRules.Should().HaveCount(2);
        enabledRules.Should().AllSatisfy(r => r.IsEnabled.Should().BeTrue());
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public async Task Rules_PersistToFile()
    {
        // Arrange
        var rule = new SimpleRule { Id = "persist-test", Name = "Persistence Test" };
        await _store.UpsertRuleAsync(rule);

        // Act - Create new store instance with same file
        var newStore = new JsonFileRuleStore(_testFilePath);
        var retrieved = await newStore.GetRuleAsync("persist-test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("persist-test");
        retrieved.Name.Should().Be("Persistence Test");
    }

    [Fact]
    public async Task FileExists_AfterFirstUpsert()
    {
        // Act
        await _store.UpsertRuleAsync(new SimpleRule { Id = "file-test" });

        // Assert
        File.Exists(_testFilePath).Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task UpsertRuleAsync_WithNullRule_Throws()
    {
        // Act & Assert
        await _store.Invoking(s => s.UpsertRuleAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetRuleAsync_WithNullId_ReturnsNull()
    {
        // Act
        var result = await _store.GetRuleAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRuleAsync_WithEmptyId_ReturnsNull()
    {
        // Act
        var result = await _store.GetRuleAsync("");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task MultipleUpserts_HandleConcurrentAccess()
    {
        // Arrange
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var ruleId = i;
            tasks.Add(_store.UpsertRuleAsync(new SimpleRule { Id = $"rule-{ruleId}" }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var rules = await _store.GetRulesAsync();
        rules.Should().HaveCount(10);
    }

    #endregion

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch { }
    }
}
