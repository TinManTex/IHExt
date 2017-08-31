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
    }
}
