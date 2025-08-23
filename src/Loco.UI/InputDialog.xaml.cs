using System.Windows;

namespace Loco.UI;

/// <summary>
/// Interaction logic for InputDialog.xaml
/// </summary>
public partial class InputDialog : Window
{
    /// <summary>
    /// Gets the input text
    /// </summary>
    public string InputText => InputTextBox.Text;
    
    /// <summary>
    /// Creates a new input dialog
    /// </summary>
    /// <param name="message">Message to display</param>
    /// <param name="title">Dialog title</param>
    /// <param name="defaultText">Default text for the input box</param>
    public InputDialog(string message, string title, string defaultText = "")
    {
        InitializeComponent();
        
        Title = title;
        MessageTextBlock.Text = message;
        InputTextBox.Text = defaultText;
        
        // Select all text by default
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
