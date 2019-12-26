using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Duplicate_Files_Cleaner
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WhatsNew_Click(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation da = new ThicknessAnimation(new Thickness(-body.ActualWidth, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(150)));
            container.BeginAnimation(MarginProperty, da);
        }

        private void Credits_Click(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation da = new ThicknessAnimation(new Thickness(-2 * body.ActualWidth, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(250)));
            container.BeginAnimation(MarginProperty, da);
        }

        private void MoreApps_Click(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation da = new ThicknessAnimation(new Thickness(-3 * body.ActualWidth, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(300)));
            container.BeginAnimation(MarginProperty, da);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation da = new ThicknessAnimation(new Thickness(0), new Duration(TimeSpan.FromMilliseconds(300)));
            container.BeginAnimation(MarginProperty, da);
        }
    }
}
