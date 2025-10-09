using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AnimeStudio.GUIV2.Views
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void CategoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            //if (e.NewValue is TreeViewItem item)
            //{
            //    CategoryTitle.Text = item.Header.ToString();
            //}
            //switch ((CategoryList.SelectedItem as ListBoxItem)?.Content.ToString())
            //{
            //    case "General": SettingsContent.Content = new GeneralSettingsControl(); break;
            //    case "Editor": SettingsContent.Content = new EditorSettingsControl(); break;
            //    case "Theme": SettingsContent.Content = new ThemeSettingsControl(); break;
            //}
        }

        private void Ok_Click(object sender, RoutedEventArgs e) { }

        private void CancelButton_Click(object sender, RoutedEventArgs e) { }

        private void ApplyButton_Click(object sender, RoutedEventArgs e) { }
    }
}
