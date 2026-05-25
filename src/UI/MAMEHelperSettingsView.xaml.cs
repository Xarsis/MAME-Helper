using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MAMEHelper.UI
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    public partial class MAMEHelperSettingsView : UserControl
    {
        private MAMEHelperSettingsViewModel _vm;

        public MAMEHelperSettingsView(MAMEHelperSettingsViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = _vm;
        }

        // ── Browse buttons ────────────────────────────────────────────────────

        private void BtnBrowseMame_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseMameExecutable();

        private void BtnBrowseListFile_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseListFile();

        private void BtnBrowseCoverFolder_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseCoverImageFolder();

        private void BtnBrowseCoverFolder2_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseCoverImageFolder2();

        private void BtnBrowseBackgroundFolder_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseBackgroundImageFolder();

        private void BtnBrowseBackgroundFolder2_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseBackgroundImageFolder2();

        // ── Save / Cancel ─────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null) window.DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null) window.DialogResult = false;
        }
    }
}
