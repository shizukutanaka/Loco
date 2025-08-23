using Loco.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loco.Web.Data;

public class SqliteFlowRepository : IFlowRepository
{
    private readonly FlowContext _context;

    public SqliteFlowRepository(FlowContext context)
    {
        _context = context;
    }

    public async Task<List<FlowDefinition>> GetAllAsync()
    {
        return await _context.Flows.AsNoTracking().ToListAsync();
    }

    public async Task<FlowDefinition?> GetByIdAsync(string id)
    {
        return await _context.Flows.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task AddAsync(FlowDefinition flow)
    {
        flow.UpdatedAt = System.DateTime.UtcNow;
        var existing = await _context.Flows.FindAsync(flow.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(flow);
        }
        else
        {
            flow.CreatedAt = System.DateTime.UtcNow;
            _context.Flows.Add(flow);
        }
        await _context.SaveChangesAsync();
    }

    public async Task IncrementDownloadsAsync(string id)
    {
        var flow = await _context.Flows.FindAsync(id);
        if (flow != null)
        {
            flow.Downloads++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var flow = await _context.Flows.FindAsync(id);
        if (flow == null)
            return false;
        _context.Flows.Remove(flow);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Flows.AsNoTracking().CountAsync();
    }
}
