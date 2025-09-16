using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces
{
    public interface IFlowValidator
    {
        Task<ValidationResult> ValidateFlowAsync(IFlow flow);
        ValidationResult ValidateFlowDefinition(FlowDefinition definition);
    }
}