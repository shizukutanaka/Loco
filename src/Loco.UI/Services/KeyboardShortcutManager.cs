using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Loco.UI.Services
{
    /// <summary>
    /// Keyboard shortcut manager following Clean Architecture principles
    /// Provides comprehensive keyboard shortcut support for improved productivity
    /// </summary>
    public sealed class KeyboardShortcutManager
    {
        private readonly Dictionary<KeyCombination, Action> _shortcuts;
        private readonly Dictionary<string, KeyCombination> _namedShortcuts;

        public KeyboardShortcutManager()
        {
            _shortcuts = new Dictionary<KeyCombination, Action>();
            _namedShortcuts = new Dictionary<string, KeyCombination>();
            RegisterDefaultShortcuts();
        }

        private void RegisterDefaultShortcuts()
        {
            // File operations
            RegisterShortcut("New", Key.N, ModifierKeys.Control, () => OnNewFile?.Invoke());
            RegisterShortcut("Open", Key.O, ModifierKeys.Control, () => OnOpen?.Invoke());
            RegisterShortcut("Save", Key.S, ModifierKeys.Control, () => OnSave?.Invoke());
            RegisterShortcut("SaveAs", Key.S, ModifierKeys.Control | ModifierKeys.Shift, () => OnSaveAs?.Invoke());
            RegisterShortcut("Close", Key.W, ModifierKeys.Control, () => OnClose?.Invoke());
            RegisterShortcut("Exit", Key.Q, ModifierKeys.Control, () => OnExit?.Invoke());

            // Edit operations
            RegisterShortcut("Undo", Key.Z, ModifierKeys.Control, () => OnUndo?.Invoke());
            RegisterShortcut("Redo", Key.Y, ModifierKeys.Control, () => OnRedo?.Invoke());
            RegisterShortcut("RedoAlt", Key.Z, ModifierKeys.Control | ModifierKeys.Shift, () => OnRedo?.Invoke());
            RegisterShortcut("Cut", Key.X, ModifierKeys.Control, () => OnCut?.Invoke());
            RegisterShortcut("Copy", Key.C, ModifierKeys.Control, () => OnCopy?.Invoke());
            RegisterShortcut("Paste", Key.V, ModifierKeys.Control, () => OnPaste?.Invoke());
            RegisterShortcut("SelectAll", Key.A, ModifierKeys.Control, () => OnSelectAll?.Invoke());
            RegisterShortcut("Find", Key.F, ModifierKeys.Control, () => OnFind?.Invoke());
            RegisterShortcut("Replace", Key.H, ModifierKeys.Control, () => OnReplace?.Invoke());

            // View operations
            RegisterShortcut("ZoomIn", Key.OemPlus, ModifierKeys.Control, () => OnZoomIn?.Invoke());
            RegisterShortcut("ZoomOut", Key.OemMinus, ModifierKeys.Control, () => OnZoomOut?.Invoke());
            RegisterShortcut("ZoomReset", Key.D0, ModifierKeys.Control, () => OnZoomReset?.Invoke());
            RegisterShortcut("ToggleFullscreen", Key.F11, ModifierKeys.None, () => OnToggleFullscreen?.Invoke());
            RegisterShortcut("ToggleSidebar", Key.B, ModifierKeys.Control, () => OnToggleSidebar?.Invoke());
            RegisterShortcut("ToggleConsole", Key.OemTilde, ModifierKeys.Control, () => OnToggleConsole?.Invoke());

            // Automation operations
            RegisterShortcut("RunAutomation", Key.R, ModifierKeys.Control, () => OnRunAutomation?.Invoke());
            RegisterShortcut("StopAutomation", Key.T, ModifierKeys.Control, () => OnStopAutomation?.Invoke());
            RegisterShortcut("NewRule", Key.N, ModifierKeys.Control | ModifierKeys.Shift, () => OnNewRule?.Invoke());
            RegisterShortcut("EditRule", Key.E, ModifierKeys.Control, () => OnEditRule?.Invoke());
            RegisterShortcut("DeleteRule", Key.Delete, ModifierKeys.None, () => OnDeleteRule?.Invoke());
            RegisterShortcut("DuplicateRule", Key.D, ModifierKeys.Control, () => OnDuplicateRule?.Invoke());
            RegisterShortcut("TestRule", Key.T, ModifierKeys.Control | ModifierKeys.Shift, () => OnTestRule?.Invoke());

            // Navigation
            RegisterShortcut("NavigateBack", Key.Left, ModifierKeys.Alt, () => OnNavigateBack?.Invoke());
            RegisterShortcut("NavigateForward", Key.Right, ModifierKeys.Alt, () => OnNavigateForward?.Invoke());
            RegisterShortcut("NavigateUp", Key.Up, ModifierKeys.Alt, () => OnNavigateUp?.Invoke());
            RegisterShortcut("NavigateHome", Key.Home, ModifierKeys.Control, () => OnNavigateHome?.Invoke());

            // Window management
            RegisterShortcut("NewWindow", Key.N, ModifierKeys.Control | ModifierKeys.Alt, () => OnNewWindow?.Invoke());
            RegisterShortcut("NextTab", Key.Tab, ModifierKeys.Control, () => OnNextTab?.Invoke());
            RegisterShortcut("PreviousTab", Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, () => OnPreviousTab?.Invoke());
            RegisterShortcut("CloseTab", Key.W, ModifierKeys.Control | ModifierKeys.Shift, () => OnCloseTab?.Invoke());

            // Help
            RegisterShortcut("Help", Key.F1, ModifierKeys.None, () => OnHelp?.Invoke());
            RegisterShortcut("ShowShortcuts", Key.OemQuestion, ModifierKeys.Control | ModifierKeys.Shift, () => OnShowShortcuts?.Invoke());
            RegisterShortcut("About", Key.F1, ModifierKeys.Shift, () => OnAbout?.Invoke());

            // Settings
            RegisterShortcut("Settings", Key.OemComma, ModifierKeys.Control, () => OnSettings?.Invoke());
            RegisterShortcut("Preferences", Key.P, ModifierKeys.Control | ModifierKeys.Alt, () => OnPreferences?.Invoke());

            // Quick actions
            RegisterShortcut("QuickAction1", Key.D1, ModifierKeys.Control | ModifierKeys.Alt, () => OnQuickAction1?.Invoke());
            RegisterShortcut("QuickAction2", Key.D2, ModifierKeys.Control | ModifierKeys.Alt, () => OnQuickAction2?.Invoke());
            RegisterShortcut("QuickAction3", Key.D3, ModifierKeys.Control | ModifierKeys.Alt, () => OnQuickAction3?.Invoke());
            RegisterShortcut("QuickAction4", Key.D4, ModifierKeys.Control | ModifierKeys.Alt, () => OnQuickAction4?.Invoke());
            RegisterShortcut("QuickAction5", Key.D5, ModifierKeys.Control | ModifierKeys.Alt, () => OnQuickAction5?.Invoke());

            // Focus management
            RegisterShortcut("FocusSearch", Key.F, ModifierKeys.Control | ModifierKeys.Shift, () => OnFocusSearch?.Invoke());
            RegisterShortcut("FocusRuleList", Key.L, ModifierKeys.Control | ModifierKeys.Shift, () => OnFocusRuleList?.Invoke());
            RegisterShortcut("FocusEditor", Key.E, ModifierKeys.Control | ModifierKeys.Shift, () => OnFocusEditor?.Invoke());
            RegisterShortcut("FocusConsole", Key.C, ModifierKeys.Control | ModifierKeys.Shift, () => OnFocusConsole?.Invoke());
        }

        /// <summary>
        /// Register a custom keyboard shortcut
        /// </summary>
        public void RegisterShortcut(string name, Key key, ModifierKeys modifiers, Action action)
        {
            var combination = new KeyCombination(key, modifiers);
            _shortcuts[combination] = action;
            _namedShortcuts[name] = combination;
        }

        /// <summary>
        /// Unregister a keyboard shortcut by name
        /// </summary>
        public void UnregisterShortcut(string name)
        {
            if (_namedShortcuts.TryGetValue(name, out var combination))
            {
                _shortcuts.Remove(combination);
                _namedShortcuts.Remove(name);
            }
        }

        /// <summary>
        /// Update an existing shortcut
        /// </summary>
        public void UpdateShortcut(string name, Key newKey, ModifierKeys newModifiers)
        {
            if (_namedShortcuts.TryGetValue(name, out var oldCombination))
            {
                if (_shortcuts.TryGetValue(oldCombination, out var action))
                {
                    _shortcuts.Remove(oldCombination);
                    var newCombination = new KeyCombination(newKey, newModifiers);
                    _shortcuts[newCombination] = action;
                    _namedShortcuts[name] = newCombination;
                }
            }
        }

        /// <summary>
        /// Handle key press event
        /// </summary>
        public bool HandleKeyPress(Key key, ModifierKeys modifiers)
        {
            var combination = new KeyCombination(key, modifiers);
            if (_shortcuts.TryGetValue(combination, out var action))
            {
                action?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get all registered shortcuts
        /// </summary>
        public Dictionary<string, string> GetShortcutList()
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in _namedShortcuts)
            {
                result[kvp.Key] = kvp.Value.ToString();
            }
            return result;
        }

        /// <summary>
        /// Check if a key combination is already registered
        /// </summary>
        public bool IsShortcutRegistered(Key key, ModifierKeys modifiers)
        {
            var combination = new KeyCombination(key, modifiers);
            return _shortcuts.ContainsKey(combination);
        }

        /// <summary>
        /// Export shortcuts to JSON
        /// </summary>
        public string ExportShortcuts()
        {
            var shortcuts = new Dictionary<string, object>();
            foreach (var kvp in _namedShortcuts)
            {
                shortcuts[kvp.Key] = new
                {
                    Key = kvp.Value.Key.ToString(),
                    Modifiers = kvp.Value.Modifiers.ToString()
                };
            }
            return System.Text.Json.JsonSerializer.Serialize(shortcuts, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        /// <summary>
        /// Import shortcuts from JSON
        /// </summary>
        public void ImportShortcuts(string json)
        {
            try
            {
                var shortcuts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ShortcutData>>(json);
                if (shortcuts != null)
                {
                    foreach (var kvp in shortcuts)
                    {
                        if (Enum.TryParse<Key>(kvp.Value.Key, out var key) &&
                            Enum.TryParse<ModifierKeys>(kvp.Value.Modifiers, out var modifiers))
                        {
                            UpdateShortcut(kvp.Key, key, modifiers);
                        }
                    }
                }
            }
            catch
            {
                // Log error or notify user
            }
        }

        // Events for each action
        public event Action OnNewFile;
        public event Action OnOpen;
        public event Action OnSave;
        public event Action OnSaveAs;
        public event Action OnClose;
        public event Action OnExit;
        public event Action OnUndo;
        public event Action OnRedo;
        public event Action OnCut;
        public event Action OnCopy;
        public event Action OnPaste;
        public event Action OnSelectAll;
        public event Action OnFind;
        public event Action OnReplace;
        public event Action OnZoomIn;
        public event Action OnZoomOut;
        public event Action OnZoomReset;
        public event Action OnToggleFullscreen;
        public event Action OnToggleSidebar;
        public event Action OnToggleConsole;
        public event Action OnRunAutomation;
        public event Action OnStopAutomation;
        public event Action OnNewRule;
        public event Action OnEditRule;
        public event Action OnDeleteRule;
        public event Action OnDuplicateRule;
        public event Action OnTestRule;
        public event Action OnNavigateBack;
        public event Action OnNavigateForward;
        public event Action OnNavigateUp;
        public event Action OnNavigateHome;
        public event Action OnNewWindow;
        public event Action OnNextTab;
        public event Action OnPreviousTab;
        public event Action OnCloseTab;
        public event Action OnHelp;
        public event Action OnShowShortcuts;
        public event Action OnAbout;
        public event Action OnSettings;
        public event Action OnPreferences;
        public event Action OnQuickAction1;
        public event Action OnQuickAction2;
        public event Action OnQuickAction3;
        public event Action OnQuickAction4;
        public event Action OnQuickAction5;
        public event Action OnFocusSearch;
        public event Action OnFocusRuleList;
        public event Action OnFocusEditor;
        public event Action OnFocusConsole;

        // Helper classes
        private struct KeyCombination : IEquatable<KeyCombination>
        {
            public Key Key { get; }
            public ModifierKeys Modifiers { get; }

            public KeyCombination(Key key, ModifierKeys modifiers)
            {
                Key = key;
                Modifiers = modifiers;
            }

            public bool Equals(KeyCombination other)
            {
                return Key == other.Key && Modifiers == other.Modifiers;
            }

            public override bool Equals(object obj)
            {
                return obj is KeyCombination other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Key, Modifiers);
            }

            public override string ToString()
            {
                var parts = new List<string>();
                
                if (Modifiers.HasFlag(ModifierKeys.Control))
                    parts.Add("Ctrl");
                if (Modifiers.HasFlag(ModifierKeys.Alt))
                    parts.Add("Alt");
                if (Modifiers.HasFlag(ModifierKeys.Shift))
                    parts.Add("Shift");
                if (Modifiers.HasFlag(ModifierKeys.Windows))
                    parts.Add("Win");
                
                parts.Add(GetKeyName(Key));
                
                return string.Join("+", parts);
            }

            private static string GetKeyName(Key key)
            {
                return key switch
                {
                    Key.OemPlus => "+",
                    Key.OemMinus => "-",
                    Key.OemComma => ",",
                    Key.OemPeriod => ".",
                    Key.OemQuestion => "?",
                    Key.OemTilde => "~",
                    Key.D0 => "0",
                    Key.D1 => "1",
                    Key.D2 => "2",
                    Key.D3 => "3",
                    Key.D4 => "4",
                    Key.D5 => "5",
                    Key.D6 => "6",
                    Key.D7 => "7",
                    Key.D8 => "8",
                    Key.D9 => "9",
                    _ => key.ToString()
                };
            }
        }

        private class ShortcutData
        {
            public string Key { get; set; }
            public string Modifiers { get; set; }
        }
    }

    /// <summary>
    /// Shortcut display helper for UI
    /// </summary>
    public static class ShortcutDisplayHelper
    {
        public static string FormatShortcut(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();
            
            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");
            
            parts.Add(GetDisplayName(key));
            
            return string.Join("+", parts);
        }

        private static string GetDisplayName(Key key)
        {
            return key switch
            {
                Key.OemPlus => "Plus",
                Key.OemMinus => "Minus",
                Key.OemComma => "Comma",
                Key.OemPeriod => "Period",
                Key.OemQuestion => "Question",
                Key.OemTilde => "Tilde",
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                Key.Delete => "Del",
                Key.Insert => "Ins",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                Key.Left => "←",
                Key.Right => "→",
                Key.Up => "↑",
                Key.Down => "↓",
                Key.Escape => "Esc",
                Key.Return => "Enter",
                Key.Space => "Space",
                Key.Tab => "Tab",
                _ => key.ToString()
            };
        }
    }
}
