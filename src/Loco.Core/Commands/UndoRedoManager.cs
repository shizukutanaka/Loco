using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Loco.Core.Commands
{
    /// <summary>
    /// Comprehensive Undo/Redo system following Command pattern
    /// Implements memory-efficient command history with configurable limits
    /// </summary>
    public sealed class UndoRedoManager : IDisposable
    {
        private readonly Stack<ICommand> _undoStack;
        private readonly Stack<ICommand> _redoStack;
        private readonly int _maxHistorySize;
        private readonly object _lock = new();
        private bool _isExecutingCommand;

        public UndoRedoManager(int maxHistorySize = 100)
        {
            _maxHistorySize = maxHistorySize > 0 ? maxHistorySize : 100;
            _undoStack = new Stack<ICommand>(_maxHistorySize);
            _redoStack = new Stack<ICommand>(_maxHistorySize);
        }

        /// <summary>
        /// Execute a command and add it to the undo stack
        /// </summary>
        public void ExecuteCommand(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            lock (_lock)
            {
                if (_isExecutingCommand)
                    return; // Prevent recursive execution

                _isExecutingCommand = true;
                try
                {
                    // Execute the command
                    command.Execute();

                    // Add to undo stack
                    _undoStack.Push(command);

                    // Clear redo stack (new action invalidates redo history)
                    _redoStack.Clear();

                    // Trim history if needed
                    TrimHistory();

                    // Raise event
                    OnCommandExecuted?.Invoke(command);
                    OnStateChanged?.Invoke();
                }
                finally
                {
                    _isExecutingCommand = false;
                }
            }
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        public bool Undo()
        {
            lock (_lock)
            {
                if (!CanUndo)
                    return false;

                _isExecutingCommand = true;
                try
                {
                    var command = _undoStack.Pop();
                    command.Undo();
                    _redoStack.Push(command);

                    OnCommandUndone?.Invoke(command);
                    OnStateChanged?.Invoke();
                    return true;
                }
                finally
                {
                    _isExecutingCommand = false;
                }
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        public bool Redo()
        {
            lock (_lock)
            {
                if (!CanRedo)
                    return false;

                _isExecutingCommand = true;
                try
                {
                    var command = _redoStack.Pop();
                    command.Execute();
                    _undoStack.Push(command);

                    OnCommandRedone?.Invoke(command);
                    OnStateChanged?.Invoke();
                    return true;
                }
                finally
                {
                    _isExecutingCommand = false;
                }
            }
        }

        /// <summary>
        /// Undo multiple commands
        /// </summary>
        public int UndoMultiple(int count)
        {
            int undoneCount = 0;
            for (int i = 0; i < count && CanUndo; i++)
            {
                if (Undo())
                    undoneCount++;
                else
                    break;
            }
            return undoneCount;
        }

        /// <summary>
        /// Redo multiple commands
        /// </summary>
        public int RedoMultiple(int count)
        {
            int redoneCount = 0;
            for (int i = 0; i < count && CanRedo; i++)
            {
                if (Redo())
                    redoneCount++;
                else
                    break;
            }
            return redoneCount;
        }

        /// <summary>
        /// Clear all history
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                // Dispose commands if they implement IDisposable
                DisposeCommands(_undoStack);
                DisposeCommands(_redoStack);

                _undoStack.Clear();
                _redoStack.Clear();

                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Get the description of the next undo command
        /// </summary>
        public string GetUndoDescription()
        {
            lock (_lock)
            {
                if (!CanUndo)
                    return null;

                return _undoStack.Peek().Description;
            }
        }

        /// <summary>
        /// Get the description of the next redo command
        /// </summary>
        public string GetRedoDescription()
        {
            lock (_lock)
            {
                if (!CanRedo)
                    return null;

                return _redoStack.Peek().Description;
            }
        }

        /// <summary>
        /// Get undo history
        /// </summary>
        public IEnumerable<string> GetUndoHistory()
        {
            lock (_lock)
            {
                foreach (var command in _undoStack)
                {
                    yield return command.Description;
                }
            }
        }

        /// <summary>
        /// Get redo history
        /// </summary>
        public IEnumerable<string> GetRedoHistory()
        {
            lock (_lock)
            {
                foreach (var command in _redoStack)
                {
                    yield return command.Description;
                }
            }
        }

        /// <summary>
        /// Create a batch command that groups multiple commands
        /// </summary>
        public BatchCommand CreateBatch(string description)
        {
            return new BatchCommand(description, this);
        }

        /// <summary>
        /// Properties
        /// </summary>
        public bool CanUndo
        {
            get
            {
                lock (_lock)
                {
                    return _undoStack.Count > 0;
                }
            }
        }

        public bool CanRedo
        {
            get
            {
                lock (_lock)
                {
                    return _redoStack.Count > 0;
                }
            }
        }

        public int UndoCount
        {
            get
            {
                lock (_lock)
                {
                    return _undoStack.Count;
                }
            }
        }

        public int RedoCount
        {
            get
            {
                lock (_lock)
                {
                    return _redoStack.Count;
                }
            }
        }

        // Events
        public event Action<ICommand> OnCommandExecuted;
        public event Action<ICommand> OnCommandUndone;
        public event Action<ICommand> OnCommandRedone;
        public event Action OnStateChanged;

        // Helper methods
        private void TrimHistory()
        {
            while (_undoStack.Count > _maxHistorySize)
            {
                // Remove oldest command from bottom of stack
                var array = _undoStack.ToArray();
                _undoStack.Clear();
                
                // Dispose the oldest command if needed
                if (array[array.Length - 1] is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                // Push back all except the oldest
                for (int i = array.Length - 2; i >= 0; i--)
                {
                    _undoStack.Push(array[i]);
                }
            }
        }

        private static void DisposeCommands(Stack<ICommand> stack)
        {
            foreach (var command in stack)
            {
                if (command is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// Base interface for all commands
    /// </summary>
    public interface ICommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }

    /// <summary>
    /// Abstract base class for commands
    /// </summary>
    public abstract class CommandBase : ICommand
    {
        public string Description { get; protected set; }

        protected CommandBase(string description)
        {
            Description = description ?? "Unnamed Command";
        }

        public abstract void Execute();
        public abstract void Undo();
    }

    /// <summary>
    /// Batch command that groups multiple commands
    /// </summary>
    public sealed class BatchCommand : ICommand, IDisposable
    {
        private readonly List<ICommand> _commands;
        private readonly UndoRedoManager _manager;
        private bool _isExecuting;

        public string Description { get; }

        internal BatchCommand(string description, UndoRedoManager manager)
        {
            Description = description;
            _manager = manager;
            _commands = new List<ICommand>();
        }

        public void Add(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (_isExecuting)
                throw new InvalidOperationException("Cannot add commands while batch is executing");

            _commands.Add(command);
        }

        public void Execute()
        {
            _isExecuting = true;
            try
            {
                foreach (var command in _commands)
                {
                    command.Execute();
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }

        public void Undo()
        {
            _isExecuting = true;
            try
            {
                // Undo in reverse order
                for (int i = _commands.Count - 1; i >= 0; i--)
                {
                    _commands[i].Undo();
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }

        public void ExecuteAsTransaction()
        {
            if (_commands.Count == 0)
                return;

            if (_commands.Count == 1)
            {
                _manager.ExecuteCommand(_commands[0]);
            }
            else
            {
                _manager.ExecuteCommand(this);
            }
        }

        public void Dispose()
        {
            foreach (var command in _commands)
            {
                if (command is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _commands.Clear();
        }
    }

    /// <summary>
    /// Delegate-based command for simple operations
    /// </summary>
    public class DelegateCommand : CommandBase
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public DelegateCommand(string description, Action execute, Action undo)
            : base(description)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public override void Execute() => _execute();
        public override void Undo() => _undo();
    }

    /// <summary>
    /// Property change command
    /// </summary>
    public class PropertyChangeCommand<T> : CommandBase
    {
        private readonly object _target;
        private readonly string _propertyName;
        private readonly T _oldValue;
        private readonly T _newValue;
        private readonly Action<object, string, T> _setter;

        public PropertyChangeCommand(
            string description,
            object target,
            string propertyName,
            T oldValue,
            T newValue,
            Action<object, string, T> setter)
            : base(description)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _oldValue = oldValue;
            _newValue = newValue;
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        }

        public override void Execute()
        {
            _setter(_target, _propertyName, _newValue);
        }

        public override void Undo()
        {
            _setter(_target, _propertyName, _oldValue);
        }
    }

    /// <summary>
    /// Collection modification commands
    /// </summary>
    public class AddItemCommand<T> : CommandBase
    {
        private readonly IList<T> _collection;
        private readonly T _item;
        private readonly int _index;

        public AddItemCommand(string description, IList<T> collection, T item, int index = -1)
            : base(description)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _item = item;
            _index = index;
        }

        public override void Execute()
        {
            if (_index >= 0 && _index <= _collection.Count)
            {
                _collection.Insert(_index, _item);
            }
            else
            {
                _collection.Add(_item);
            }
        }

        public override void Undo()
        {
            _collection.Remove(_item);
        }
    }

    public class RemoveItemCommand<T> : CommandBase
    {
        private readonly IList<T> _collection;
        private readonly T _item;
        private int _index;

        public RemoveItemCommand(string description, IList<T> collection, T item)
            : base(description)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _item = item;
        }

        public override void Execute()
        {
            _index = _collection.IndexOf(_item);
            if (_index >= 0)
            {
                _collection.RemoveAt(_index);
            }
        }

        public override void Undo()
        {
            if (_index >= 0)
            {
                _collection.Insert(_index, _item);
            }
        }
    }

    public class MoveItemCommand<T> : CommandBase
    {
        private readonly IList<T> _collection;
        private readonly int _fromIndex;
        private readonly int _toIndex;

        public MoveItemCommand(string description, IList<T> collection, int fromIndex, int toIndex)
            : base(description)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _fromIndex = fromIndex;
            _toIndex = toIndex;
        }

        public override void Execute()
        {
            if (_fromIndex < 0 || _fromIndex >= _collection.Count ||
                _toIndex < 0 || _toIndex >= _collection.Count)
                return;

            var item = _collection[_fromIndex];
            _collection.RemoveAt(_fromIndex);
            _collection.Insert(_toIndex, item);
        }

        public override void Undo()
        {
            if (_toIndex < 0 || _toIndex >= _collection.Count ||
                _fromIndex < 0 || _fromIndex >= _collection.Count)
                return;

            var item = _collection[_toIndex];
            _collection.RemoveAt(_toIndex);
            _collection.Insert(_fromIndex, item);
        }
    }

    /// <summary>
    /// Macro command that records and replays a sequence of commands
    /// </summary>
    public class MacroCommand : CommandBase
    {
        private readonly List<ICommand> _commands;

        public MacroCommand(string description)
            : base(description)
        {
            _commands = new List<ICommand>();
        }

        public void AddCommand(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            _commands.Add(command);
        }

        public override void Execute()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        public override void Undo()
        {
            // Undo in reverse order
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

        public void Clear()
        {
            _commands.Clear();
        }
    }
}
