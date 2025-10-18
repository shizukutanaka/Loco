using System;
using System.Collections.Generic;
using System.Threading;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Execution context for a workflow, containing variables and state.
    /// </summary>
    public class WorkflowContext : IDisposable
    {
        private readonly Dictionary<string, object?> _variables;
        private readonly Dictionary<string, object?> _metadata;
        private bool _disposed;

        public string WorkflowId { get; }
        public DateTime StartTime { get; }
        public CancellationToken CancellationToken { get; }

        public WorkflowContext(string workflowId, CancellationToken cancellationToken = default)
        {
            WorkflowId = workflowId;
            StartTime = DateTime.UtcNow;
            CancellationToken = cancellationToken;
            _variables = new Dictionary<string, object?>();
            _metadata = new Dictionary<string, object?>();
        }

        /// <summary>
        /// Sets a variable in the workflow context.
        /// </summary>
        public void SetVariable(string name, object? value)
        {
            ThrowIfDisposed();
            _variables[name] = value;
        }

        /// <summary>
        /// Gets a variable from the workflow context.
        /// </summary>
        public T? GetVariable<T>(string name, T? defaultValue = default)
        {
            ThrowIfDisposed();
            
            if (_variables.TryGetValue(name, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                
                // Try to convert
                try
                {
                    return (T?)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            
            return defaultValue;
        }

        /// <summary>
        /// Checks if a variable exists.
        /// </summary>
        public bool HasVariable(string name)
        {
            ThrowIfDisposed();
            return _variables.ContainsKey(name);
        }

        /// <summary>
        /// Removes a variable.
        /// </summary>
        public bool RemoveVariable(string name)
        {
            ThrowIfDisposed();
            return _variables.Remove(name);
        }

        /// <summary>
        /// Gets all variable names.
        /// </summary>
        public IEnumerable<string> GetVariableNames()
        {
            ThrowIfDisposed();
            return _variables.Keys;
        }

        /// <summary>
        /// Sets metadata.
        /// </summary>
        public void SetMetadata(string key, object? value)
        {
            ThrowIfDisposed();
            _metadata[key] = value;
        }

        /// <summary>
        /// Gets metadata.
        /// </summary>
        public T? GetMetadata<T>(string key, T? defaultValue = default)
        {
            ThrowIfDisposed();
            
            if (_metadata.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
            }
            
            return defaultValue;
        }

        /// <summary>
        /// Clears all variables.
        /// </summary>
        public void ClearVariables()
        {
            ThrowIfDisposed();
            _variables.Clear();
        }

        /// <summary>
        /// Gets elapsed time since workflow start.
        /// </summary>
        public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WorkflowContext));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _variables.Clear();
                _metadata.Clear();
                _disposed = true;
            }
        }
    }
}
