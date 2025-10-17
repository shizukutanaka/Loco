using System;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Design tokens based on Atlassian Design System principles
    /// Provides consistent spacing, colors, and typography for CLI
    /// </summary>
    public static class DesignTokens
    {
        /// <summary>
        /// Spacing scale following 4px/8px grid system
        /// </summary>
        public static class Spacing
        {
            public const int None = 0;
            public const int XXSmall = 2;   // 0.125rem equivalent
            public const int XSmall = 4;    // 0.25rem
            public const int Small = 8;     // 0.5rem
            public const int Medium = 12;   // 0.75rem
            public const int Large = 16;    // 1rem
            public const int XLarge = 24;   // 1.5rem
            public const int XXLarge = 32;  // 2rem
            public const int XXXLarge = 48; // 3rem

            /// <summary>
            /// Get spacing string for console formatting
            /// </summary>
            public static string Get(int size) => new string(' ', size);
        }

        /// <summary>
        /// Color tokens with semantic meanings
        /// Inspired by Atlassian's color system
        /// </summary>
        public static class Colors
        {
            // Brand colors
            public static class Brand
            {
                public static ConsoleColor Primary = ConsoleColor.Blue;
                public static ConsoleColor Secondary = ConsoleColor.Cyan;
            }

            // Semantic colors
            public static class Semantic
            {
                public static ConsoleColor Success = ConsoleColor.Green;
                public static ConsoleColor Warning = ConsoleColor.Yellow;
                public static ConsoleColor Error = ConsoleColor.Red;
                public static ConsoleColor Critical = ConsoleColor.Magenta;
                public static ConsoleColor Info = ConsoleColor.Blue;
                public static ConsoleColor Discovery = ConsoleColor.DarkMagenta;
            }

            // Neutral colors
            public static class Neutral
            {
                public static ConsoleColor Text = ConsoleColor.White;
                public static ConsoleColor TextSubtle = ConsoleColor.Gray;
                public static ConsoleColor TextDisabled = ConsoleColor.DarkGray;
                public static ConsoleColor Border = ConsoleColor.DarkGray;
                public static ConsoleColor Background = ConsoleColor.Black;
                public static ConsoleColor BackgroundSubtle = ConsoleColor.DarkGray;
            }

            // Interactive states
            public static class Interactive
            {
                public static ConsoleColor Default = ConsoleColor.White;
                public static ConsoleColor Hover = ConsoleColor.Cyan;
                public static ConsoleColor Active = ConsoleColor.Blue;
                public static ConsoleColor Focus = ConsoleColor.Yellow;
                public static ConsoleColor Disabled = ConsoleColor.DarkGray;
            }
        }

        /// <summary>
        /// Border styles and characters
        /// </summary>
        public static class Borders
        {
            public static class Box
            {
                public const char TopLeft = '┌';
                public const char TopRight = '┐';
                public const char BottomLeft = '└';
                public const char BottomRight = '┘';
                public const char Horizontal = '─';
                public const char Vertical = '│';
                public const char CrossLeft = '├';
                public const char CrossRight = '┤';
                public const char CrossTop = '┬';
                public const char CrossBottom = '┴';
                public const char Cross = '┼';
            }

            public static class BoxBold
            {
                public const char TopLeft = '╔';
                public const char TopRight = '╗';
                public const char BottomLeft = '╚';
                public const char BottomRight = '╝';
                public const char Horizontal = '═';
                public const char Vertical = '║';
            }

            public static class BoxDouble
            {
                public const char TopLeft = '╔';
                public const char TopRight = '╗';
                public const char BottomLeft = '╚';
                public const char BottomRight = '╝';
                public const char Horizontal = '═';
                public const char Vertical = '║';
            }

            public static class BoxRounded
            {
                public const char TopLeft = '╭';
                public const char TopRight = '╮';
                public const char BottomLeft = '╰';
                public const char BottomRight = '╯';
                public const char Horizontal = '─';
                public const char Vertical = '│';
            }
        }

        /// <summary>
        /// Icon set following Atlassian iconography
        /// </summary>
        public static class Icons
        {
            // Status icons
            public const string Success = "✓";
            public const string Error = "✗";
            public const string Warning = "⚠";
            public const string Info = "ℹ";
            public const string Help = "?";

            // Navigation icons
            public const string ArrowRight = "→";
            public const string ArrowLeft = "←";
            public const string ArrowUp = "↑";
            public const string ArrowDown = "↓";

            // Action icons
            public const string Add = "+";
            public const string Remove = "-";
            public const string Edit = "✎";
            public const string Delete = "🗑";
            public const string Copy = "⎘";
            public const string Search = "🔍";

            // State icons
            public const string Check = "✔";
            public const string Cross = "✘";
            public const string Star = "★";
            public const string StarOutline = "☆";
            public const string Circle = "●";
            public const string CircleOutline = "○";
            public const string Square = "■";
            public const string SquareOutline = "□";

            // Misc icons
            public const string Bullet = "•";
            public const string Clock = "⏱";
            public const string Calendar = "📅";
            public const string File = "📄";
            public const string Folder = "📁";
            public const string Gear = "⚙";
            public const string Lock = "🔒";
            public const string Unlock = "🔓";
            public const string User = "👤";
            public const string Users = "👥";
            public const string Rocket = "🚀";
            public const string Lightning = "⚡";
            public const string Fire = "🔥";
        }

        /// <summary>
        /// Typography settings
        /// </summary>
        public static class Typography
        {
            public static class Size
            {
                public const string Small = "Small";     // For hints, captions
                public const string Body = "Body";       // Default text
                public const string Large = "Large";     // For emphasis
                public const string XLarge = "XLarge";   // For headings
                public const string XXLarge = "XXLarge"; // For titles
            }

            public static class Weight
            {
                public const string Regular = "Regular";
                public const string Medium = "Medium";
                public const string Bold = "Bold";
            }
        }

        /// <summary>
        /// Elevation levels (visual hierarchy)
        /// Represented through spacing and borders
        /// </summary>
        public static class Elevation
        {
            public const int Flat = 0;
            public const int Raised = 1;
            public const int Overlay = 2;
            public const int Modal = 3;
        }

        /// <summary>
        /// Animation timing
        /// </summary>
        public static class Animation
        {
            public const int Fast = 80;      // Quick transitions
            public const int Normal = 150;   // Standard animations
            public const int Slow = 300;     // Deliberate movements
            public const int Entrance = 200; // Component entrance
            public const int Exit = 150;     // Component exit
        }

        /// <summary>
        /// Layout constants
        /// </summary>
        public static class Layout
        {
            public const int MinWidth = 40;
            public const int StandardWidth = 80;
            public const int MaxWidth = 120;
            public const int CompactHeight = 10;
            public const int StandardHeight = 24;
            public const int ExpandedHeight = 40;
        }

        /// <summary>
        /// Component sizes
        /// </summary>
        public static class ComponentSize
        {
            public const string Small = "small";
            public const string Medium = "medium";
            public const string Large = "large";
        }

        /// <summary>
        /// Z-index layers
        /// </summary>
        public static class ZIndex
        {
            public const int Base = 0;
            public const int Dropdown = 100;
            public const int Sticky = 200;
            public const int Fixed = 300;
            public const int ModalBackdrop = 400;
            public const int Modal = 500;
            public const int Popover = 600;
            public const int Tooltip = 700;
        }

        /// <summary>
        /// Accessibility labels
        /// </summary>
        public static class Accessibility
        {
            public const string RequiredMarker = "*";
            public const string OptionalMarker = "(optional)";
            public const string ExpandedMarker = "[expanded]";
            public const string CollapsedMarker = "[collapsed]";
            public const string SelectedMarker = "[selected]";
        }
    }
}
