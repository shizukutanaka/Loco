using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Web.Data;

public interface IFlowRepository
{
    Task<List<FlowDefinition>> GetAllAsync();
    Task<FlowDefinition?> GetByIdAsync(string id);
    Task AddAsync(FlowDefinition flow);
    Task IncrementDownloadsAsync(string id);
    Task<bool> DeleteAsync(string id);
    Task<int> CountAsync();
}
