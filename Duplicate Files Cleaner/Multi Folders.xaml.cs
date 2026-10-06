using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ookii.Dialogs.Wpf;
using System.IO;

namespace Duplicate_Files_Cleaner
{
    /// <summary>
    /// Interaction logic for Multi_Folders.xaml
    /// </summary>
    public partial class Multi_Folders : Window
    {
        public Multi_Folders()
        {
            InitializeComponent();
            list_of_directories.DataContext = list_to_add;
        }

        ObservableCollection<ListItem> list_to_add = new ObservableCollection<ListItem>();
        bool accept = false;

        internal bool ShowDialog(List<string> paths)
        {
            ShowDialog();
            if (accept)
            {
                foreach (var i in list_to_add)
                    paths.Add(i.FullPath);
            }
            return accept;
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (list_of_directories.Items.Count > 0)
            {
                accept = true;
                Close();
            }
            else
            {
                Functions.ShuckWindow(this, list_of_directories.Items.Count == 0, "List is empty!");
                Done.IsEnabled = false;
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (object bw_sender, DoWorkEventArgs args) => { Thread.Sleep(300); };
                bw.RunWorkerCompleted += (object completed_sender, RunWorkerCompletedEventArgs completed_e) => { Done.IsEnabled = true; };
                bw.RunWorkerAsync();
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog fbd = new VistaFolderBrowserDialog();
            if (fbd.ShowDialog() == true)
                AddDirectoryToList(fbd.SelectedPath);
        }

        private void AddDirectoryToList(string path)
        {
            if (Directory.Exists(path))
            {
                bool? isExist = false;  //true:Exist  false:notExist  null:is a father of one of directory in the list
                if (path[path.Length - 1] != '\\') //add slash to end of the path, then we can check if directory is exist or its a child of any existing directory
                    path = path + @"\";
                foreach (ListItem i in list_to_add)
                    if (path.ToLower().Contains(i.FullPath.ToLower()))
                    {
                        isExist = true;
                        break;
                    }
                    else if (i.FullPath.ToLower().Contains(path.ToLower()))
                    {
                        isExist = null;
                        break;
                    }
                if (isExist == false)
                    list_to_add.Add(new ListItem(path, null));
                else if (isExist == true)
                    MessageBox.Show("Directory is already Exist in the list or in a directory in the list!", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show("you have first to remove all '" + path + "' suns from the list", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
                MessageBox.Show("Path: '" + path + "' has not been found!", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            list_to_add.Clear();
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            for (int i = list_of_directories.SelectedItems.Count - 1; i > -1; i--)
                list_to_add.Remove((ListItem)list_of_directories.SelectedItems[i]);
        }

        private void ListOfDirectories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (list_of_directories.SelectedItems.Count > 0)
                remove_selected.IsEnabled = true;
            else
                remove_selected.IsEnabled = false;
        }

        private void AddFolderFromPath_Click(object sender, RoutedEventArgs e)
        {
            AddDirectoryToList(to_add_path.Text);
        }

        private void Search_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AddDirectoryToList(to_add_path.Text);
        }
    }
}
