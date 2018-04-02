using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IHExt {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            this.AnnounceLogBlockout.Visibility = Visibility.Hidden;
            this.runningLabel.Visibility = Visibility.Hidden;
            this.menuTitle.Visibility = Visibility.Hidden;
            this.menuItems.Visibility = Visibility.Hidden;
            this.menuTestWrap.Visibility = Visibility.Hidden;
            this.menuWrap.Visibility = Visibility.Hidden;
            this.menuHelp.Visibility = Visibility.Hidden;
        }

        void TextControl_OnEnter(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                TextBox textBox = (TextBox)sender;

                var app = (App)Application.Current;
                app.ToMgsvCmd("input|"+textBox.Name+"|"+textBox.Text);

                textBox.Text = String.Empty;

                e.Handled = true;
            }
        }

        public void ListBox_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox != null)
            {
                ListBoxItem lbi = ((sender as ListBox).SelectedItem as ListBoxItem);
                int selectedIndex = listBox.SelectedIndex;

                if (selectedIndex == -1)//tex no item selected
                {
                } else {
                    var app = (App)Application.Current;
                    app.ToMgsvCmd("selected|" + listBox.Name + "|" + selectedIndex.ToString());
                }

                e.Handled = true;
            }
        }

        public void ListBox_OnDoubleClick(object sender, RoutedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox != null)
            {
                ListBoxItem lbi = (listBox.SelectedItem as ListBoxItem);
                int selectedIndex = listBox.SelectedIndex;

                if (selectedIndex == -1)//tex no item selected
                {
                } else
                {
                    var app = (App)Application.Current;
                    app.ToMgsvCmd("activate|" + listBox.Name + "|" + selectedIndex.ToString());
                }

                e.Handled = true;
            }
        }

        public void ComboBox_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                ComboBoxItem lbi = (comboBox.SelectedItem as ComboBoxItem);
                int selectedIndex = comboBox.SelectedIndex;

                if (selectedIndex == -1)//tex no item selected
                {
                } else
                {
                    var app = (App)Application.Current;
                    app.ToMgsvCmd("selectedcombo|" + comboBox.Name + "|" + selectedIndex.ToString());
                }

                e.Handled = true;
            }
        }

        private void minButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            minButton.Visibility = Visibility.Visible;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            minButton.Visibility = Visibility.Hidden;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                var app = (App)Application.Current;
                app.ToMgsvCmd("togglemenu");
            }
        }

        private void ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ComboBox comboBox = (ComboBox)sender;

                var app = (App)Application.Current;
                app.ToMgsvCmd("input|" + comboBox.Name + "|" + comboBox.Text);

                comboBox.Text = String.Empty;

                e.Handled = true;
            }
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //tex user edited combobox
            ComboBox comboBox = (ComboBox)sender;
            if (comboBox.SelectedIndex == -1)
            {
                var app = (App)Application.Current;
                app.ToMgsvCmd("comboboxtocurrent|" + comboBox.Name + "|" + comboBox.Text);

                e.Handled = true;
            }
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var app = (App)Application.Current;
            app.ToMgsvCmd("GotKeyboardFocus|" + menuLine.Name);
            menuLine.Select(0, menuLine.Text.Length);

            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var app = (App)Application.Current;
                app.ToMgsvCmd("EnterText|" + menuLine.Name + "|" + menuLine.Text);
            }
            //DEBUGNOW e.Handled = true;
        }
    }
}
