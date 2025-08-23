using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.UI
{
    /// <summary>
    /// Manages application themes including dark/light mode
    /// Following Rob Pike's simplicity with practical theming
    /// </summary>
    public interface IThemeService
    {
        Theme CurrentTheme { get; }
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        Task<bool> SetThemeAsync(string themeName);
        Task<Theme> GetThemeAsync(string themeName);
        Task<List<Theme>> GetAvailableThemesAsync();
        Task<bool> CreateCustomThemeAsync(Theme theme);
        Task<bool> DeleteCustomThemeAsync(string themeName);
    }

    public class ThemeService : IThemeService
    {
        private readonly ILogger<ThemeService> _logger;
        private readonly Dictionary<string, Theme> _themes;
        private Theme _currentTheme;

        public Theme CurrentTheme => _currentTheme;
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger;
            _themes = InitializeDefaultThemes();
            _currentTheme = _themes["light"];
        }

        public async Task<bool> SetThemeAsync(string themeName)
        {
            try
            {
                if (!_themes.TryGetValue(themeName.ToLower(), out var theme))
                {
                    _logger.LogWarning("Theme {ThemeName} not found", themeName);
                    return false;
                }

                var previousTheme = _currentTheme;
                _currentTheme = theme;

                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs
                {
                    PreviousTheme = previousTheme,
                    NewTheme = theme
                });

                _logger.LogInformation("Theme changed to {ThemeName}", themeName);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set theme {ThemeName}", themeName);
                return false;
            }
        }

        public async Task<Theme> GetThemeAsync(string themeName)
        {
            return await Task.FromResult(
                _themes.TryGetValue(themeName.ToLower(), out var theme) ? theme : null);
        }

        public async Task<List<Theme>> GetAvailableThemesAsync()
        {
            return await Task.FromResult(_themes.Values.ToList());
        }

        public async Task<bool> CreateCustomThemeAsync(Theme theme)
        {
            try
            {
                if (theme == null || string.IsNullOrEmpty(theme.Name))
                {
                    return false;
                }

                var key = theme.Name.ToLower();
                if (_themes.ContainsKey(key))
                {
                    _logger.LogWarning("Theme {ThemeName} already exists", theme.Name);
                    return false;
                }

                _themes[key] = theme;
                _logger.LogInformation("Created custom theme {ThemeName}", theme.Name);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create custom theme");
                return false;
            }
        }

        public async Task<bool> DeleteCustomThemeAsync(string themeName)
        {
            try
            {
                var key = themeName.ToLower();
                
                // Don't allow deletion of default themes
                if (key == "light" || key == "dark" || key == "high-contrast")
                {
                    _logger.LogWarning("Cannot delete default theme {ThemeName}", themeName);
                    return false;
                }

                if (_themes.Remove(key))
                {
                    _logger.LogInformation("Deleted custom theme {ThemeName}", themeName);
                    
                    // Switch to light theme if current theme was deleted
                    if (_currentTheme.Name.ToLower() == key)
                    {
                        await SetThemeAsync("light");
                    }
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete custom theme {ThemeName}", themeName);
                return false;
            }
        }

        private Dictionary<string, Theme> InitializeDefaultThemes()
        {
            return new Dictionary<string, Theme>
            {
                ["light"] = new Theme
                {
                    Name = "Light",
                    Description = "Default light theme",
                    Colors = new ThemeColors
                    {
                        Primary = "#007ACC",
                        Secondary = "#40E0D0",
                        Background = "#FFFFFF",
                        Surface = "#F5F5F5",
                        Text = "#212121",
                        TextSecondary = "#757575",
                        Error = "#F44336",
                        Warning = "#FF9800",
                        Success = "#4CAF50",
                        Info = "#2196F3",
                        Border = "#E0E0E0"
                    },
                    Typography = new ThemeTypography
                    {
                        FontFamily = "Segoe UI, Roboto, sans-serif",
                        FontSizeBase = 14,
                        FontSizeSmall = 12,
                        FontSizeLarge = 16,
                        FontSizeH1 = 32,
                        FontSizeH2 = 24,
                        FontSizeH3 = 20
                    }
                },
                ["dark"] = new Theme
                {
                    Name = "Dark",
                    Description = "Dark mode theme",
                    Colors = new ThemeColors
                    {
                        Primary = "#90CAF9",
                        Secondary = "#CE93D8",
                        Background = "#121212",
                        Surface = "#1E1E1E",
                        Text = "#FFFFFF",
                        TextSecondary = "#B0B0B0",
                        Error = "#CF6679",
                        Warning = "#FFB74D",
                        Success = "#81C784",
                        Info = "#64B5F6",
                        Border = "#333333"
                    },
                    Typography = new ThemeTypography
                    {
                        FontFamily = "Segoe UI, Roboto, sans-serif",
                        FontSizeBase = 14,
                        FontSizeSmall = 12,
                        FontSizeLarge = 16,
                        FontSizeH1 = 32,
                        FontSizeH2 = 24,
                        FontSizeH3 = 20
                    }
                },
                ["high-contrast"] = new Theme
                {
                    Name = "High Contrast",
                    Description = "High contrast theme for accessibility",
                    Colors = new ThemeColors
                    {
                        Primary = "#FFFF00",
                        Secondary = "#00FFFF",
                        Background = "#000000",
                        Surface = "#1A1A1A",
                        Text = "#FFFFFF",
                        TextSecondary = "#FFFF00",
                        Error = "#FF0000",
                        Warning = "#FFA500",
                        Success = "#00FF00",
                        Info = "#00BFFF",
                        Border = "#FFFFFF"
                    },
                    Typography = new ThemeTypography
                    {
                        FontFamily = "Segoe UI, Roboto, sans-serif",
                        FontSizeBase = 16,
                        FontSizeSmall = 14,
                        FontSizeLarge = 18,
                        FontSizeH1 = 36,
                        FontSizeH2 = 28,
                        FontSizeH3 = 22
                    }
                }
            };
        }
    }

    public class Theme
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ThemeColors Colors { get; set; }
        public ThemeTypography Typography { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    public class ThemeColors
    {
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Background { get; set; }
        public string Surface { get; set; }
        public string Text { get; set; }
        public string TextSecondary { get; set; }
        public string Error { get; set; }
        public string Warning { get; set; }
        public string Success { get; set; }
        public string Info { get; set; }
        public string Border { get; set; }
    }

    public class ThemeTypography
    {
        public string FontFamily { get; set; }
        public int FontSizeBase { get; set; }
        public int FontSizeSmall { get; set; }
        public int FontSizeLarge { get; set; }
        public int FontSizeH1 { get; set; }
        public int FontSizeH2 { get; set; }
        public int FontSizeH3 { get; set; }
    }

    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme PreviousTheme { get; set; }
        public Theme NewTheme { get; set; }
    }
}
