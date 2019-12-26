using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ookii.Dialogs.Wpf;

namespace Duplicate_Files_Cleaner
{
    /// <summary>
    /// Interaction logic for Settnigs.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
        }

        List<int> buffers = new List<int>() { 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304, 8388608, 16777216, 33554432 };

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.language = language.SelectedIndex;
            Properties.Settings.Default.system_files = system_files.IsChecked.Value;
            Properties.Settings.Default.hidden_files = hidden_files.IsChecked.Value;
            Properties.Settings.Default.html_files = html_files.IsChecked.Value;
            Properties.Settings.Default.special_folder = special_folder.IsChecked.Value;
            Properties.Settings.Default.buffer_size = buffers[Convert.ToInt32(buffer_slider.Value)];
            
            List<string> pathes = new List<string>();
            string excluded_path = "";
            int count = 0;
            special_folder_list.Text = special_folder_list.Text.Replace("\r\n", "\n");
            special_folder_list.Text = special_folder_list.Text.Replace("\r", "\n");
            foreach (string i in special_folder_list.Text.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                count++;
                if (Directory.Exists(i))
                {
                    string path = i.EndsWith("\\") ? i : i + "\\";
                    int exists = 0;
                    for (int j = pathes.Count - 1; j > -1; j--)
                    {
                        if (pathes[j].Contains(path))
                        {
                            exists =1;
                            pathes.RemoveAt(j);
                            
                        }
                        else if (path.Contains(pathes[j]))
                        {
                            exists = 2;
                            break;
                        }
                    }
                    if (exists < 2)
                        pathes.Add(path);
                }
                    
                if (count > 50)
                    break;
            }
            foreach (var i in pathes)
                excluded_path += (i + Environment.NewLine);

            excluded_path = excluded_path.TrimEnd('\r', 'n');

            Properties.Settings.Default.special_folder_list = excluded_path;
            if (do_full_scan.IsChecked.Value)
                Properties.Settings.Default.scan_quality = 4;
            else if (smart_scan.IsChecked.Value)
                Properties.Settings.Default.scan_quality = 3;
            else if (fast_scan.IsChecked.Value)
                Properties.Settings.Default.scan_quality = 2;
            else if (file_size_scan.IsChecked.Value)
                Properties.Settings.Default.scan_quality = 1;

            if (file_name.IsChecked.Value)
                Properties.Settings.Default.keep_file_name = (byte)(file_name_combo.SelectedIndex + 1);
            else
                Properties.Settings.Default.keep_file_name = 0;

            if (file_path.IsChecked.Value)
                Properties.Settings.Default.keep_file_path = (byte)(file_path_combo.SelectedIndex + 1);
            else
                Properties.Settings.Default.keep_file_path = 0;

            if (creating_date.IsChecked.Value)
                Properties.Settings.Default.creating_date = (byte)(creating_date_combo.SelectedIndex + 1);
            else
                Properties.Settings.Default.creating_date = 0;

            if (modified_date.IsChecked.Value)
                Properties.Settings.Default.modified_date = (byte)(modified_date_combo.SelectedIndex + 1);
            else
                Properties.Settings.Default.modified_date = 0;

            Properties.Settings.Default.name_contains = name_contains.IsChecked.Value;
            Properties.Settings.Default.name_contains_chars = new string(name_contains_list.Text.ToCharArray().Distinct().ToArray()); ;

            Properties.Settings.Default.Save();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
          if(  MessageBox.Show("Do you want to restore the default settings?","Settings",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
            {
                Properties.Settings.Default.Reset();

                FirstRun();

                Properties.Settings.Default.Save();
                Close();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog fbd = new VistaFolderBrowserDialog();
            fbd.Description = "Select Folder to add to excluded list:";
            fbd.ShowNewFolderButton = false;
            if(fbd.ShowDialog()==true)
                special_folder_list.AppendText(fbd.SelectedPath + Environment.NewLine);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FirstRun();
            language.SelectedIndex = Properties.Settings.Default.language;
            system_files.IsChecked = Properties.Settings.Default.system_files;
            hidden_files.IsChecked = Properties.Settings.Default.hidden_files;
            html_files.IsChecked = Properties.Settings.Default.html_files;
            special_folder.IsChecked = Properties.Settings.Default.special_folder;

            int buffer = buffers.IndexOf(Properties.Settings.Default.buffer_size + 1);
            buffer_slider.Value = buffer < 1 ? 6 : buffer;

            special_folder_list.Text = "";
            foreach (string i in Properties.Settings.Default.special_folder_list.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                if (Directory.Exists(i))
                    special_folder_list.Text += (i + Environment.NewLine);
            }
            special_folder_list.Text = special_folder_list.Text.TrimEnd('\r', '\n');
            Properties.Settings.Default.special_folder_list = special_folder_list.Text;
            Properties.Settings.Default.Save();
            switch(Properties.Settings.Default.scan_quality)
            {
                case 4:
                    do_full_scan.IsChecked = true;
                    break;
                case 3:
                    smart_scan.IsChecked = true;
                    break;
                case 2:
                    fast_scan.IsChecked = true;
                    break;
                case 1:
                    file_size_scan.IsChecked = true;
                    break;
            }
            special_folder.IsChecked = Properties.Settings.Default.special_folder;

            file_name.IsChecked = Properties.Settings.Default.keep_file_name > 0;
            file_name_combo.SelectedIndex = Properties.Settings.Default.keep_file_name == 2 ? 1 : 0;

            file_path.IsChecked = Properties.Settings.Default.keep_file_path > 0;
            file_path_combo.SelectedIndex = Properties.Settings.Default.keep_file_path == 2 ? 1 : 0;

            creating_date.IsChecked = Properties.Settings.Default.creating_date > 0;
            creating_date_combo.SelectedIndex = Properties.Settings.Default.creating_date == 2 ? 1 :0;

            modified_date.IsChecked = Properties.Settings.Default.modified_date > 0;
            modified_date_combo.SelectedIndex = Properties.Settings.Default.modified_date == 2 ? 1 : 0;

            name_contains.IsChecked = Properties.Settings.Default.name_contains;
            name_contains_list.Text = Properties.Settings.Default.name_contains_chars;
        }

        internal static void FirstRun()
        {
            if (Properties.Settings.Default.first_run)
            {
                string excluded_path = "";
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)))
                    excluded_path += Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
                    excluded_path += Environment.NewLine + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Environment.NewLine;
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)))
                    excluded_path += Environment.NewLine + Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
                    excluded_path += Environment.NewLine + Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
                    if (!Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).Equals(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
                        excluded_path += Environment.NewLine + Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
                    excluded_path += Environment.NewLine + Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                Properties.Settings.Default.special_folder_list = excluded_path;

                Properties.Settings.Default.first_run = false;

                Properties.Settings.Default.Save();
            }
        }

        private void BufferSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (buffer_textblock != null)
                buffer_textblock.Text = buffers[(int)e.NewValue - 1] / 1024 + " KB";
        }
    }
}
