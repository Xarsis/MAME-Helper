using System.Windows;
using System.Windows.Input;

namespace MAMEHelper.UI
{
    /// <summary>
    /// A simple modal dialog that prompts the user for a single text value.
    /// Usage:
    ///   var dlg = new InputDialog("Enter category name:", "My Category");
    ///   if (dlg.ShowDialog() == true) { string value = dlg.InputValue; }
    /// </summary>
    public partial class InputDialog : Window
    {
        public string InputValue => TxtInput.Text.Trim();

        public InputDialog(string prompt, string defaultValue = "")
        {
            InitializeComponent();
            LblPrompt.Text  = prompt;
            TxtInput.Text   = defaultValue;
            TxtInput.SelectAll();
            Loaded += (s, e) => TxtInput.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  DialogResult = true;
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
