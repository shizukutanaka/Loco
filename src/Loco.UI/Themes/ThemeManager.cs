using System;
using System.Windows;
using System.Windows.Media;

namespace Loco.UI.Themes
{
    /// <summary>
    /// Manages application themes (light/dark mode)
    /// </summary>
    public static class ThemeManager
    {
        private static Theme _currentTheme = Theme.Light;
        
        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        
        /// <summary>
        /// Gets or sets the current theme
        /// </summary>
        public static Theme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme();
                    ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(value));
                }
            }
        }
        
        /// <summary>
        /// Toggles between light and dark themes
        /// </summary>
        public static void ToggleTheme()
        {
            CurrentTheme = _currentTheme == Theme.Light ? Theme.Dark : Theme.Light;
        }
        
        /// <summary>
        /// Applies the current theme to the application
        /// </summary>
        private static void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null) return;
            
            // Clear existing theme resources
            app.Resources.MergedDictionaries.Clear();
            
            // Load the appropriate theme resource dictionary
            var themeDict = new ResourceDictionary();
            themeDict.Source = new Uri(
                $"pack://application:,,,/Loco.UI;component/Themes/{_currentTheme}Theme.xaml", 
                UriKind.Absolute);
            app.Resources.MergedDictionaries.Add(themeDict);

            // Always merge shared control styles that depend on theme brushes
            var controlsDict = new ResourceDictionary();
            controlsDict.Source = new Uri(
                "pack://application:,,,/Loco.UI;component/Themes/Controls.xaml",
                UriKind.Absolute);
            app.Resources.MergedDictionaries.Add(controlsDict);
        }
    }
    
    /// <summary>
    /// Represents the available themes
    /// </summary>
    public enum Theme
    {
        Light,
        Dark
    }
    
    /// <summary>
    /// Event arguments for theme change events
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme NewTheme { get; }
        
        public ThemeChangedEventArgs(Theme newTheme)
        {
            NewTheme = newTheme;
        }
    }
}
