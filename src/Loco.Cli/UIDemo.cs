using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Cli.UI;

namespace Loco.Cli
{
    /// <summary>
    /// Demonstration of Atlassian-inspired UI components
    /// Run: Loco.Cli.exe demo
    /// </summary>
    public static class UIDemo
    {
        public static async Task RunAsync()
        {
            Console.Clear();

            // Banner
            Components.Banner.Info("Welcome to Loco UI Component Demo");

            Console.WriteLine("This demo showcases the Atlassian Design System-inspired components.\n");

            await Task.Delay(1000);

            // Section: Badges and Lozenges
            Components.SectionFlag.Show("Badges & Lozenges", DesignTokens.Colors.Brand.Primary);

            Console.Write("Status badges: ");
            Components.Badge.Success("Active");
            Console.Write(" ");
            Components.Badge.Warning("Pending");
            Console.Write(" ");
            Components.Badge.Error("Failed");
            Console.Write(" ");
            Components.Badge.Info("Info");
            Console.WriteLine("\n");

            Console.Write("Lozenges: ");
            Components.Lozenge.Success("Success");
            Console.Write(" ");
            Components.Lozenge.Warning("Warning");
            Console.Write(" ");
            Components.Lozenge.Error("Error");
            Console.Write(" ");
            Components.Lozenge.Default("Default");
            Console.WriteLine("\n");

            await Task.Delay(1000);

            // Section: Cards
            Components.SectionFlag.Show("Card Component", DesignTokens.Colors.Brand.Primary);

            Components.Card.Show(
                "System Information",
                "OS: Windows 10\nCPU: Intel Core i7\nRAM: 16 GB\nDisk: 512 GB SSD",
                width: 50
            );

            await Task.Delay(1000);

            // Section: Progress
            Components.SectionFlag.Show("Progress Indicators", DesignTokens.Colors.Brand.Primary);

            for (int i = 0; i <= 100; i += 10)
            {
                Components.ProgressIndicator.Show(i, 100, "Processing");
                await Task.Delay(200);
            }
            Components.ProgressIndicator.Complete("Processing complete!");

            Console.WriteLine();
            await Task.Delay(1000);

            // Section: Breadcrumb
            Components.SectionFlag.Show("Navigation", DesignTokens.Colors.Brand.Primary);

            Console.Write("Breadcrumb: ");
            Components.Breadcrumb.Show(new[] { "Home", "Projects", "Loco", "UI Components" });

            Console.WriteLine();
            await Task.Delay(1000);

            // Section: Dividers
            Components.SectionFlag.Show("Dividers", DesignTokens.Colors.Brand.Primary);

            Console.WriteLine("Standard divider:");
            Components.Divider.Show();

            Console.WriteLine("Thick divider:");
            Components.Divider.Thick();

            Console.WriteLine("Dotted divider:");
            Components.Divider.Dotted();

            Console.WriteLine();
            await Task.Delay(1000);

            // Section: Inline Dialog
            Components.SectionFlag.Show("Inline Dialog", DesignTokens.Colors.Brand.Primary);

            Components.InlineDialog.Show(
                "Quick Tip",
                "You can use keyboard shortcuts to navigate faster. Press Ctrl+H for help.",
                DesignTokens.Colors.Semantic.Info
            );

            await Task.Delay(1000);

            // Section: Tables (Responsive)
            Components.SectionFlag.Show("Responsive Table", DesignTokens.Colors.Brand.Primary);

            var headers = new[] { "Name", "Status", "Last Updated", "Actions" };
            var rows = new List<string[]>
            {
                new[] { "Task 1", "✓ Complete", "2 hours ago", "5" },
                new[] { "Task 2", "⏱ In Progress", "30 min ago", "3" },
                new[] { "Task 3", "○ Pending", "1 day ago", "0" }
            };

            Layout.ResponsiveTable.Show(headers, rows);

            Console.WriteLine();
            await Task.Delay(1000);

            // Section: Layout Systems
            Components.SectionFlag.Show("Layout Systems", DesignTokens.Colors.Brand.Primary);

            Console.WriteLine("Two-column layout:");
            Layout.Columns.Show(
                ("Left Column\nContent here", 1),
                ("Right Column\nMore content", 1)
            );

            Console.WriteLine();
            await Task.Delay(1000);

            // Section: Accessibility
            Components.SectionFlag.Show("Accessibility Features", DesignTokens.Colors.Brand.Primary);

            Accessibility.ShowKeyboardShortcuts(new Dictionary<string, string>
            {
                { "Ctrl+H", "Show help" },
                { "Ctrl+C", "Cancel operation" },
                { "Tab", "Auto-complete" },
                { "↑/↓", "Navigate history" }
            });

            await Task.Delay(1000);

            // Section: Design Tokens
            Components.SectionFlag.Show("Design Tokens", DesignTokens.Colors.Brand.Primary);

            Console.WriteLine("Spacing scale:");
            Console.WriteLine($"  Small:  {DesignTokens.Spacing.Small}px");
            Console.WriteLine($"  Medium: {DesignTokens.Spacing.Medium}px");
            Console.WriteLine($"  Large:  {DesignTokens.Spacing.Large}px");

            Console.WriteLine("\nIcons:");
            Console.WriteLine($"  {DesignTokens.Icons.Success} Success");
            Console.WriteLine($"  {DesignTokens.Icons.Error} Error");
            Console.WriteLine($"  {DesignTokens.Icons.Warning} Warning");
            Console.WriteLine($"  {DesignTokens.Icons.Info} Info");
            Console.WriteLine($"  {DesignTokens.Icons.ArrowRight} Arrow");
            Console.WriteLine($"  {DesignTokens.Icons.Rocket} Rocket");
            Console.WriteLine($"  {DesignTokens.Icons.Lightning} Lightning");

            Console.WriteLine();

            // Section: Advanced Components
            Components.SectionFlag.Show("Advanced Components", DesignTokens.Colors.Brand.Primary);

            // Tabs
            Console.WriteLine("Tabs Component:");
            AdvancedComponents.Tabs.Show(
                new[] { "Overview", "Configuration", "Logs", "Settings" },
                new[] { "Welcome to the Overview tab!\nHere you can see system status and metrics." }
            );

            await Task.Delay(1000);

            // Accordion
            Console.WriteLine("\nAccordion Component:");
            AdvancedComponents.Accordion.Show(new Dictionary<string, string>
            {
                ["Getting Started"] = "Quick introduction to Loco\nInstall, configure, and run your first automation",
                ["Advanced Features"] = "Plugins, custom rules, LLM integration",
                ["Troubleshooting"] = "Common issues and solutions"
            });

            await Task.Delay(1000);

            // Data Grid
            Components.SectionFlag.Show("Data Grid", DesignTokens.Colors.Brand.Primary);

            var sampleData = new List<SampleItem>
            {
                new SampleItem { Id = "item-001", Name = "Web Server", Status = "Running", Uptime = "99.9%" },
                new SampleItem { Id = "item-002", Name = "Database", Status = "Running", Uptime = "99.8%" },
                new SampleItem { Id = "item-003", Name = "Cache", Status = "Warning", Uptime = "98.5%" }
            };

            AdvancedComponents.DataGrid.Show(sampleData, new Dictionary<string, AdvancedComponents.DataGrid.Column>
            {
                ["Id"] = new() { Header = "ID", Width = 12 },
                ["Name"] = new() { Header = "Service", Width = 15 },
                ["Status"] = new() { Header = "Status", Width = 12 },
                ["Uptime"] = new() { Header = "Uptime", Width = 10 }
            });

            await Task.Delay(1000);

            // Toast notification
            Components.SectionFlag.Show("Toast Notifications", DesignTokens.Colors.Brand.Primary);
            Console.WriteLine("Watch the bottom-right corner...\n");

            AdvancedComponents.Toast.Show("Operation completed successfully!", "success", 2000);
            await Task.Delay(2500);

            AdvancedComponents.Toast.Show("Warning: Low disk space", "warning", 2000);
            await Task.Delay(2500);

            // Modal demo (optional)
            Console.WriteLine("\nWould you like to see a modal dialog? (y/N): ");
            var showModal = Console.ReadLine()?.ToLower();

            if (showModal == "y" || showModal == "yes")
            {
                var result = AdvancedComponents.Modal.Show(
                    "Confirmation Required",
                    "Are you sure you want to proceed with this action? This cannot be undone.",
                    new[] { "Yes, Continue", "Cancel" }
                );

                if (result)
                {
                    AdvancedComponents.Toast.Show("Action confirmed!", "success");
                }
                else
                {
                    AdvancedComponents.Toast.Show("Action cancelled", "info");
                }

                await Task.Delay(2000);
            }

            // Final banner
            Components.Banner.Success("Demo Complete! All components rendered successfully.");

            Console.WriteLine("\nTerminal Info:");
            Console.WriteLine($"  Size Category: {Layout.GetTerminalSize()}");
            Console.WriteLine($"  Content Width: {Layout.GetContentWidth()}px");
            Console.WriteLine($"  Window Size: {Console.WindowWidth}x{Console.WindowHeight}");
            Console.WriteLine();

            Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
            Console.WriteLine("Component Summary:");
            Console.ResetColor();
            Console.WriteLine($"  {DesignTokens.Icons.Success} Basic Components: 9");
            Console.WriteLine($"  {DesignTokens.Icons.Success} Advanced Components: 6");
            Console.WriteLine($"  {DesignTokens.Icons.Success} Layout Systems: 5");
            Console.WriteLine($"  {DesignTokens.Icons.Success} Design Tokens: 50+");

            Console.WriteLine("\n" + DesignTokens.Spacing.Get(DesignTokens.Spacing.Medium) +
                            "Press any key to return to main menu...");
            Console.ReadKey(true);
        }

        private class SampleItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Status { get; set; } = "";
            public string Uptime { get; set; } = "";
        }
    }
}
