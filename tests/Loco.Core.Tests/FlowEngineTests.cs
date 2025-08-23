using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Interfaces;

namespace Loco.Core.Tests
{
    public class FlowEngineTests
    {
        private readonly Mock<ILogger<FlowEngine>> _loggerMock;
        private readonly FlowEngine _flowEngine;

        public FlowEngineTests()
        {
            _loggerMock = new Mock<ILogger<FlowEngine>>();
            _flowEngine = new FlowEngine(_loggerMock.Object);
        }

        [Fact]
        public async Task RegisterFlow_Should_Add_Flow_Successfully()
        {
            // Arrange
            var flow = new FlowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Flow",
                Description = "Test Description"
            };

            // Act
            var result = await _flowEngine.RegisterFlowAsync(flow);

            // Assert
            result.Should().BeTrue();
            _flowEngine.GetFlowCount().Should().Be(1);
        }

        [Fact]
        public async Task RegisterFlow_Should_Reject_Duplicate_Flow()
        {
            // Arrange
            var flowId = Guid.NewGuid().ToString();
            var flow1 = new FlowDefinition { Id = flowId, Name = "Flow 1" };
            var flow2 = new FlowDefinition { Id = flowId, Name = "Flow 2" };

            // Act
            await _flowEngine.RegisterFlowAsync(flow1);
            var result = await _flowEngine.RegisterFlowAsync(flow2);

            // Assert
            result.Should().BeFalse();
            _flowEngine.GetFlowCount().Should().Be(1);
        }

        [Fact]
        public async Task ExecuteFlow_Should_Return_False_For_NonExistent_Flow()
        {
            // Arrange
            var nonExistentFlowId = Guid.NewGuid().ToString();

            // Act
            var result = await _flowEngine.ExecuteFlowAsync(nonExistentFlowId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UnregisterFlow_Should_Remove_Flow_Successfully()
        {
            // Arrange
            var flow = new FlowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Flow"
            };
            await _flowEngine.RegisterFlowAsync(flow);

            // Act
            var result = await _flowEngine.UnregisterFlowAsync(flow.Id);

            // Assert
            result.Should().BeTrue();
            _flowEngine.GetFlowCount().Should().Be(0);
        }

        [Fact]
        public async Task GetFlow_Should_Return_Correct_Flow()
        {
            // Arrange
            var flow = new FlowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Flow",
                Description = "Test Description"
            };
            await _flowEngine.RegisterFlowAsync(flow);

            // Act
            var retrievedFlow = await _flowEngine.GetFlowAsync(flow.Id);

            // Assert
            retrievedFlow.Should().NotBeNull();
            retrievedFlow!.Id.Should().Be(flow.Id);
            retrievedFlow.Name.Should().Be(flow.Name);
            retrievedFlow.Description.Should().Be(flow.Description);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task RegisterMultipleFlows_Should_Track_Count_Correctly(int flowCount)
        {
            // Arrange & Act
            for (int i = 0; i < flowCount; i++)
            {
                var flow = new FlowDefinition
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Flow {i}"
                };
                await _flowEngine.RegisterFlowAsync(flow);
            }

            // Assert
            _flowEngine.GetFlowCount().Should().Be(flowCount);
        }

        [Fact]
        public async Task ExecuteFlow_Should_Handle_Exceptions_Gracefully()
        {
            // Arrange
            var flow = new FlowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Faulty Flow",
                Actions = new List<FlowAction>
                {
                    new FlowAction { Type = "invalid_action" }
                }
            };
            await _flowEngine.RegisterFlowAsync(flow);

            // Act
            Func<Task> act = async () => await _flowEngine.ExecuteFlowAsync(flow.Id);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
