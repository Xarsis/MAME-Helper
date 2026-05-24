using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MAMEHelper.UI
{
    /// <summary>Converts bool to its inverse — used to enable/disable controls based on radio selection.</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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

        private void BtnBrowseMame_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseMameExecutable();

        private void BtnBrowseListFile_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseListFile();

        private void BtnBrowseCoverFolder_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseCoverImageFolder();

        private void BtnBrowseBackgroundFolder_Click(object sender, RoutedEventArgs e)
            => _vm.BrowseBackgroundImageFolder();
    }
}
