using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Interfaces;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Wraps an action to add simple conditional execution based on RunIf/SkipIf.
    /// </summary>
    public class SimpleConditionalAction : IAction
    {
        private readonly IAction _innerAction;
        private readonly string? _runIf;
        private readonly string? _skipIf;

        public string Id => _innerAction.Id;
        public string Name => _innerAction.Name;

        public SimpleConditionalAction(IAction innerAction, string? runIf, string? skipIf)
        {
            _innerAction = innerAction;
            _runIf = runIf;
            _skipIf = skipIf;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            // Evaluate conditions
            if (!ConditionalExecutor.ShouldExecute(_runIf, _skipIf, context.Variables))
            {
                // Mark as skipped but successful
                context.Variables[$"{Id}_skipped"] = true;
                return true;
            }

            // Execute the wrapped action
            var result = await _innerAction.ExecuteAsync(context);

            // Record execution result
            context.Variables[$"{Id}_success"] = result;

            return result;
        }
    }
}
