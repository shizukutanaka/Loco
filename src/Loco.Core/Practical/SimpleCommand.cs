// Rob Pike: "Don't communicate by sharing memory; share memory by communicating"
// Robert C. Martin: "A command should do one thing and be easily testable"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple command pattern - Encapsulate operations, support undo, enable queuing
/// Fast, testable, zero dependencies
/// </summary>
public interface ICommand
{
    Task<bool> ExecuteAsync();
    bool CanExecute();
}

public interface IUndoableCommand : ICommand
{
    Task<bool> UndoAsync();
    bool CanUndo();
}

/// <summary>
/// Simple command executor - Executes commands with history
/// </summary>
public class CommandExecutor
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly SimpleLogger _logger;
    private readonly int _maxHistory;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public CommandExecutor(int maxHistory = 100, SimpleLogger? logger = null)
    {
        _maxHistory = maxHistory;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(CommandExecutor));
    }

    public async Task<bool> ExecuteAsync(ICommand command)
    {
        if (!command.CanExecute())
        {
            _logger.Warning($"Command {command.GetType().Name} cannot execute");
            return false;
        }

        try
        {
            var result = await command.ExecuteAsync();

            if (result && command is IUndoableCommand undoable)
            {
                _undoStack.Push(undoable);
                _redoStack.Clear(); // Clear redo stack on new command

                // Limit history size
                while (_undoStack.Count > _maxHistory && _undoStack.Count > 0)
                {
                    var items = _undoStack.ToArray();
                    _undoStack.Clear();
                    for (int i = 1; i < items.Length; i++) // Skip first (oldest)
                    {
                        _undoStack.Push(items[i]);
                    }
                }
            }

            _logger.Info($"Command {command.GetType().Name} executed: {result}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Command {command.GetType().Name} failed", ex);
            return false;
        }
    }

    public async Task<bool> UndoAsync()
    {
        if (_undoStack.Count == 0)
        {
            _logger.Warning("No commands to undo");
            return false;
        }

        var command = _undoStack.Pop();

        if (!command.CanUndo())
        {
            _logger.Warning($"Command {command.GetType().Name} cannot undo");
            return false;
        }

        try
        {
            var result = await command.UndoAsync();

            if (result)
            {
                _redoStack.Push(command);
            }

            _logger.Info($"Command {command.GetType().Name} undone: {result}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Undo {command.GetType().Name} failed", ex);
            return false;
        }
    }

    public async Task<bool> RedoAsync()
    {
        if (_redoStack.Count == 0)
        {
            _logger.Warning("No commands to redo");
            return false;
        }

        var command = _redoStack.Pop();
        return await ExecuteAsync(command);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>
/// Command queue - Process commands asynchronously
/// </summary>
public class CommandQueue
{
    private readonly FastQueue<ICommand> _queue;
    private readonly CommandExecutor _executor;
    private readonly SimpleBackgroundTaskRunner _runner;
    private readonly SimpleLogger _logger;
    private readonly CancellationTokenSource _cts = new();

    public int QueueLength => _queue.Count;
    public bool IsProcessing { get; private set; }

    public CommandQueue(int capacity = 1000, SimpleLogger? logger = null)
    {
        _queue = new FastQueue<ICommand>(capacity);
        _executor = new CommandExecutor(logger: logger);
        _runner = new SimpleBackgroundTaskRunner();
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(CommandQueue));
    }

    public async Task<bool> EnqueueAsync(ICommand command)
    {
        if (!await _queue.EnqueueAsync(command))
        {
            _logger.Warning("Command queue is full");
            return false;
        }

        if (!IsProcessing)
        {
            StartProcessing();
        }

        return true;
    }

    private void StartProcessing()
    {
        IsProcessing = true;

        _runner.RunAsync(async ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                var command = await _queue.DequeueAsync(1000);
                if (command != null)
                {
                    await _executor.ExecuteAsync(command);
                }
            }
            IsProcessing = false;
        }, _cts.Token, "CommandProcessor");
    }

    public void StopProcessing()
    {
        _cts.Cancel();
        IsProcessing = false;
    }

    public void Dispose()
    {
        StopProcessing();
        _runner.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Batch command - Execute multiple commands as one
/// </summary>
public class BatchCommand : IUndoableCommand
{
    private readonly List<ICommand> _commands;
    private readonly List<ICommand> _executedCommands = new();
    private readonly bool _stopOnFailure;

    public BatchCommand(IEnumerable<ICommand> commands, bool stopOnFailure = true)
    {
        _commands = commands.ToList();
        _stopOnFailure = stopOnFailure;
    }

    public async Task<bool> ExecuteAsync()
    {
        _executedCommands.Clear();

        foreach (var command in _commands)
        {
            if (!command.CanExecute())
            {
                if (_stopOnFailure) return false;
                continue;
            }

            var result = await command.ExecuteAsync();
            if (result)
            {
                _executedCommands.Add(command);
            }
            else if (_stopOnFailure)
            {
                // Rollback executed commands
                await RollbackAsync();
                return false;
            }
        }

        return _executedCommands.Count > 0;
    }

    public bool CanExecute() => _commands.Any(c => c.CanExecute());

    public async Task<bool> UndoAsync()
    {
        // Undo in reverse order
        for (int i = _executedCommands.Count - 1; i >= 0; i--)
        {
            if (_executedCommands[i] is IUndoableCommand undoable && undoable.CanUndo())
            {
                await undoable.UndoAsync();
            }
        }

        return true;
    }

    public bool CanUndo() => _executedCommands.OfType<IUndoableCommand>().Any(c => c.CanUndo());

    private async Task RollbackAsync()
    {
        await UndoAsync();
        _executedCommands.Clear();
    }
}

/// <summary>
/// Retry command decorator - Adds retry logic to any command
/// </summary>
public class RetryCommand : ICommand
{
    private readonly ICommand _innerCommand;
    private readonly int _maxAttempts;
    private readonly int _delayMs;

    public RetryCommand(ICommand innerCommand, int maxAttempts = 3, int delayMs = 100)
    {
        _innerCommand = innerCommand;
        _maxAttempts = maxAttempts;
        _delayMs = delayMs;
    }

    public async Task<bool> ExecuteAsync()
    {
        for (int i = 0; i < _maxAttempts; i++)
        {
            if (await _innerCommand.ExecuteAsync())
            {
                return true;
            }

            if (i < _maxAttempts - 1)
            {
                await Task.Delay(_delayMs * (i + 1)); // Exponential backoff
            }
        }

        return false;
    }

    public bool CanExecute() => _innerCommand.CanExecute();
}

/// <summary>
/// Example: File operation commands
/// </summary>
public class CreateFileCommand : IUndoableCommand
{
    private readonly string _path;
    private readonly string _content;
    private bool _fileCreated;

    public CreateFileCommand(string path, string content)
    {
        _path = path;
        _content = content;
    }

    public async Task<bool> ExecuteAsync()
    {
        if (File.Exists(_path))
        {
            return false;
        }

        await File.WriteAllTextAsync(_path, _content);
        _fileCreated = true;
        return true;
    }

    public bool CanExecute() => !string.IsNullOrEmpty(_path) && !File.Exists(_path);

    public async Task<bool> UndoAsync()
    {
        if (_fileCreated && File.Exists(_path))
        {
            File.Delete(_path);
            _fileCreated = false;
            return true;
        }
        return false;
    }

    public bool CanUndo() => _fileCreated && File.Exists(_path);
}

public class DeleteFileCommand : IUndoableCommand
{
    private readonly string _path;
    private string? _backupContent;

    public DeleteFileCommand(string path)
    {
        _path = path;
    }

    public async Task<bool> ExecuteAsync()
    {
        if (!File.Exists(_path))
        {
            return false;
        }

        _backupContent = await File.ReadAllTextAsync(_path);
        File.Delete(_path);
        return true;
    }

    public bool CanExecute() => File.Exists(_path);

    public async Task<bool> UndoAsync()
    {
        if (_backupContent != null && !File.Exists(_path))
        {
            await File.WriteAllTextAsync(_path, _backupContent);
            return true;
        }
        return false;
    }

    public bool CanUndo() => _backupContent != null && !File.Exists(_path);
}

/// <summary>
/// Example: Database commands
/// </summary>
public class DatabaseCommand : IUndoableCommand
{
    private readonly string _query;
    private readonly string _undoQuery;
    private readonly Func<string, Task<bool>> _executeQuery;

    public DatabaseCommand(
        string query,
        string undoQuery,
        Func<string, Task<bool>> executeQuery)
    {
        _query = query;
        _undoQuery = undoQuery;
        _executeQuery = executeQuery;
    }

    public async Task<bool> ExecuteAsync() => await _executeQuery(_query);
    public bool CanExecute() => !string.IsNullOrEmpty(_query);
    public async Task<bool> UndoAsync() => await _executeQuery(_undoQuery);
    public bool CanUndo() => !string.IsNullOrEmpty(_undoQuery);
}

/// <summary>
/// Example: Macro command recorder
/// </summary>
public class MacroRecorder
{
    private readonly List<ICommand> _recordedCommands = new();
    private bool _isRecording;

    public bool IsRecording => _isRecording;
    public int CommandCount => _recordedCommands.Count;

    public void StartRecording()
    {
        _isRecording = true;
        _recordedCommands.Clear();
    }

    public void RecordCommand(ICommand command)
    {
        if (_isRecording)
        {
            _recordedCommands.Add(command);
        }
    }

    public MacroCommand StopRecording(string name)
    {
        _isRecording = false;
        return new MacroCommand(name, _recordedCommands.ToList());
    }

    public class MacroCommand : IUndoableCommand
    {
        private readonly string _name;
        private readonly BatchCommand _batch;

        public string Name => _name;

        public MacroCommand(string name, List<ICommand> commands)
        {
            _name = name;
            _batch = new BatchCommand(commands, stopOnFailure: false);
        }

        public Task<bool> ExecuteAsync() => _batch.ExecuteAsync();
        public bool CanExecute() => _batch.CanExecute();
        public Task<bool> UndoAsync() => _batch.UndoAsync();
        public bool CanUndo() => _batch.CanUndo();
    }
}

/// <summary>
/// Example: Text editor commands
/// </summary>
public class TextEditor
{
    private readonly CommandExecutor _executor = new();
    private string _text = "";

    public string Text => _text;

    public async Task InsertTextAsync(string text, int position)
    {
        var command = new InsertTextCommand(this, text, position);
        await _executor.ExecuteAsync(command);
    }

    public async Task DeleteTextAsync(int position, int length)
    {
        var command = new DeleteTextCommand(this, position, length);
        await _executor.ExecuteAsync(command);
    }

    public async Task<bool> UndoAsync() => await _executor.UndoAsync();
    public async Task<bool> RedoAsync() => await _executor.RedoAsync();

    private class InsertTextCommand : IUndoableCommand
    {
        private readonly TextEditor _editor;
        private readonly string _text;
        private readonly int _position;

        public InsertTextCommand(TextEditor editor, string text, int position)
        {
            _editor = editor;
            _text = text;
            _position = position;
        }

        public Task<bool> ExecuteAsync()
        {
            _editor._text = _editor._text.Insert(_position, _text);
            return Task.FromResult(true);
        }

        public bool CanExecute() => _position >= 0 && _position <= _editor._text.Length;

        public Task<bool> UndoAsync()
        {
            _editor._text = _editor._text.Remove(_position, _text.Length);
            return Task.FromResult(true);
        }

        public bool CanUndo() => true;
    }

    private class DeleteTextCommand : IUndoableCommand
    {
        private readonly TextEditor _editor;
        private readonly int _position;
        private readonly int _length;
        private string? _deletedText;

        public DeleteTextCommand(TextEditor editor, int position, int length)
        {
            _editor = editor;
            _position = position;
            _length = length;
        }

        public Task<bool> ExecuteAsync()
        {
            _deletedText = _editor._text.Substring(_position, _length);
            _editor._text = _editor._text.Remove(_position, _length);
            return Task.FromResult(true);
        }

        public bool CanExecute() =>
            _position >= 0 &&
            _length > 0 &&
            _position + _length <= _editor._text.Length;

        public Task<bool> UndoAsync()
        {
            if (_deletedText != null)
            {
                _editor._text = _editor._text.Insert(_position, _deletedText);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public bool CanUndo() => _deletedText != null;
    }
}