using System;
using System.Collections.Generic;
using Loco.Core.Commands;

namespace Loco.UI.Commands
{
    /// <summary>
    /// Manages command execution with undo/redo support
    /// Uses the Core UndoRedoManager for actual implementation
    /// </summary>
    public class CommandManager
    {
        private readonly UndoRedoManager _undoRedoManager;
        
        public CommandManager(int maxHistorySize = 100)
        {
            _undoRedoManager = new UndoRedoManager(maxHistorySize);
            
            // Forward events
            _undoRedoManager.OnCommandExecuted += cmd => CommandExecuted?.Invoke(cmd);
            _undoRedoManager.OnCommandUndone += cmd => CommandUndone?.Invoke(cmd);
            _undoRedoManager.OnCommandRedone += cmd => CommandRedone?.Invoke(cmd);
            _undoRedoManager.OnStateChanged += () => StateChanged?.Invoke();
        }
        
        /// <summary>
        /// Execute a command and add it to the history
        /// </summary>
        public void ExecuteCommand(ICommand command)
        {
            _undoRedoManager.ExecuteCommand(command);
        }
        
        /// <summary>
        /// Undo the last command
        /// </summary>
        public bool Undo()
        {
            return _undoRedoManager.Undo();
        }
        
        /// <summary>
        /// Redo the last undone command
        /// </summary>
        public bool Redo()
        {
            return _undoRedoManager.Redo();
        }
        
        /// <summary>
        /// Clear the command history
        /// </summary>
        public void Clear()
        {
            _undoRedoManager.Clear();
        }
        
        /// <summary>
        /// Check if undo is available
        /// </summary>
        public bool CanUndo => _undoRedoManager.CanUndo;
        
        /// <summary>
        /// Check if redo is available
        /// </summary>
        public bool CanRedo => _undoRedoManager.CanRedo;
        
        /// <summary>
        /// Get the description of the next undo command
        /// </summary>
        public string GetUndoDescription() => _undoRedoManager.GetUndoDescription();
        
        /// <summary>
        /// Get the description of the next redo command
        /// </summary>
        public string GetRedoDescription() => _undoRedoManager.GetRedoDescription();
        
        /// <summary>
        /// Get the undo history
        /// </summary>
        public IEnumerable<string> GetUndoHistory() => _undoRedoManager.GetUndoHistory();
        
        /// <summary>
        /// Get the redo history
        /// </summary>
        public IEnumerable<string> GetRedoHistory() => _undoRedoManager.GetRedoHistory();
        
        /// <summary>
        /// Create a batch command
        /// </summary>
        public BatchCommand CreateBatch(string description) => _undoRedoManager.CreateBatch(description);
        
        // Events
        public event Action<ICommand> CommandExecuted;
        public event Action<ICommand> CommandUndone;
        public event Action<ICommand> CommandRedone;
        public event Action StateChanged;
    }
}
