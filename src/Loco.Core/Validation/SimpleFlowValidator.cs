using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core.Validation
{
    public class SimpleFlowValidator : IFlowValidator
    {
        public async Task<ValidationResult> ValidateFlowAsync(IFlow flow)
        {
            var result = new ValidationResult();

            if (flow == null)
            {
                result.AddError("flow", "Flow cannot be null");
                return result;
            }

            // Validate basic properties
            if (string.IsNullOrWhiteSpace(flow.Id))
                result.AddError("id", "Flow ID is required");

            if (string.IsNullOrWhiteSpace(flow.Name))
                result.AddError("name", "Flow name is required");

            // Validate using flow's own validation
            try
            {
                var flowValidation = await flow.ValidateAsync();
                if (!flowValidation.IsValid)
                {
                    foreach (var error in flowValidation.Errors)
                    {
                        result.AddError(error, error);
                    }
                }
            }
            catch (System.Exception ex)
            {
                result.AddError("validation", $"Flow validation error: {ex.Message}");
            }

            return result;
        }

        public ValidationResult ValidateFlowDefinition(FlowDefinition definition)
        {
            var result = new ValidationResult();

            if (definition == null)
            {
                result.AddError("definition", "Flow definition cannot be null");
                return result;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
                result.AddError("id", "Flow definition ID is required");

            if (string.IsNullOrWhiteSpace(definition.Name))
                result.AddError("name", "Flow definition name is required");

            if (definition.Triggers == null || definition.Triggers.Count == 0)
                result.AddError("triggers", "Flow must have at least one trigger");

            if (definition.Actions == null || definition.Actions.Count == 0)
                result.AddError("actions", "Flow must have at least one action");

            return result;
        }
    }
}