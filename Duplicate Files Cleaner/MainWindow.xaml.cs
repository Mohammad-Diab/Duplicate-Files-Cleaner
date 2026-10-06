using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using System.Threading.Tasks;

namespace Duplicate_Files_Cleaner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        #region Initializing

        List<DuplicateFiles_ListView> duplicate_data = new List<DuplicateFiles_ListView>();
        DuplicateFileRemoverPages current_page = DuplicateFileRemoverPages.DuplicateFind;
        DuplicateFileRemoverPages CurrentPage { get => current_page; set => current_page = value; }

        Duplicate finder = new Duplicate(32768);

        bool sun_bridle = true, bridle_father = true, show_message_again = true, starting_window = true;
        double duplicate_size = 0;
        double deleted_size = 0;
        double total_size = 0;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            rec.Width = label1.ActualWidth;
            item_group_viewer.DataContext = duplicate_data;
            starting_window = false;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!starting_window)
            {
                double t = (-1 * ((int)CurrentPage - 1)) * e.NewSize.Width;
                ThicknessAnimation ta = new ThicknessAnimation(new Thickness(t, 0, 0, 0), new Duration(new TimeSpan(0)));
                mother.BeginAnimation(MarginProperty, ta);
                for (int i = 0; i < mother.Children.Count; i++)
                {
                    ((Grid)mother.Children[i]).Width = ActualWidth - 5;
                    ((Grid)mother.Children[i]).Margin = new Thickness(i * e.NewSize.Width, 0, 0, 0);
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (finder.IsON || IsSame.IsON)
            {
                if (MessageBox.Show("Operation is running, Stop it?", "Stop Operation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    IsSame.CancelOperation();
                    finder.CancelOperation();
                }
                else
                    e.Cancel = true;
            }
        }

        void TabChange_MouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            ThicknessAnimation ta = new ThicknessAnimation(new Thickness((-1 * tab_panal.Children.IndexOf((Label)sender)) * ActualWidth, 0, 0, 0), new Duration(new TimeSpan(2000000)));
            mother.BeginAnimation(MarginProperty, ta);
            Functions.TabChange(tab_panal, (Label)sender, rec, (int)CurrentPage - 1, tab_panal.Children.IndexOf((Label)sender));
            CurrentPage = (DuplicateFileRemoverPages)tab_panal.Children.IndexOf((Label)sender) + 1;
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            new Settings().ShowDialog();
        }

        private void AboutTool_Click(object sender, RoutedEventArgs e)
        {
            new About().ShowDialog();
        }

        #endregion


        #region Checker

        private void Sun_Unchecked(object sender, RoutedEventArgs e)
        {
            if (bridle_father)
                sun_bridle = false;
            CheckAllSun(duplicate_data[item_group_viewer.SelectedIndex], -1);
            sun_bridle = true;
        }

        private void Sun_Checked(object sender, RoutedEventArgs e)
        {
            if (bridle_father)
                sun_bridle = false;
            if (duplicate_data[item_group_viewer.SelectedIndex].cheched_count == duplicate_data[item_group_viewer.SelectedIndex].Duplicats.Count - 1)
                if (show_message_again)
                {
                    if (MessageBox.Show("You are about to remove all copies of this file, do you want to do that?", "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        CheckAllSun(duplicate_data[item_group_viewer.SelectedIndex], 1);
                    else
                    {
                        sun_bridle = true;
                        bridle_father = false;
                        (sender as CheckBox).IsChecked = false;
                        bridle_father = true;
                    }
                }
                else
                    CheckAllSun(duplicate_data[item_group_viewer.SelectedIndex], 1);
            else
                CheckAllSun(duplicate_data[item_group_viewer.SelectedIndex], 1);
            sun_bridle = true;
        }

        private void CheckAllSun(DuplicateFiles_ListView d, int dif)
        {
            if (!sun_bridle)
            {
                duplicate_data[item_group_viewer.SelectedIndex].cheched_count += dif;
                deleted_size += dif * duplicate_data[item_group_viewer.SelectedIndex].Size;
                deleted_grid.Width = (deleted_size / total_size) * total.ActualWidth + 4;
                deleted_text.Text = Functions.FileSize(deleted_size, 2) + " will be deleted";
                int count = 0;
                foreach (var i in d.Duplicats)
                    if (i.IsChecked) count++;
                if (count == 0)
                    d.ChangeIsCheakedValue(false);
                else if (count == d.Duplicats.Count)
                    d.ChangeIsCheakedValue(true);
                else
                    d.ChangeIsCheakedValue(null);
                item_group_viewer.Items.Refresh();
                item_viewer.Items.Refresh();
            }
        }

        #endregion


        #region Sorter

        DuplicateSortBy sort_by = DuplicateSortBy.Count;
        DuplicateSortBy SortBy
        {
            get { return sort_by; }
            set
            {
                if (sort_by - value == 4 || sort_by - value == -4)
                    duplicate_data.Reverse();
                else
                {
                    sort_by = value;
                    duplicate_data.Sort(Sorter);
                }
                sort_by = value;
                item_group_viewer.Items.Refresh();
            }
        }

        private int Sorter(DuplicateFiles_ListView x, DuplicateFiles_ListView y)
        {
            switch (SortBy)
            {
                case DuplicateSortBy.Count:
                    return x.Duplicats.Count.CompareTo(y.Duplicats.Count);
                case DuplicateSortBy.DecCount:
                    return -1 * (x.Duplicats.Count.CompareTo(y.Duplicats.Count));

                case DuplicateSortBy.Size:
                    return x.Size.CompareTo(y.Size);
                case DuplicateSortBy.DecSize:
                    return y.Size.CompareTo(x.Size);

                case DuplicateSortBy.TotalSize:
                    return (x.Size * x.Duplicats.Count).CompareTo(y.Size * y.Duplicats.Count);
                case DuplicateSortBy.DecTotalSize:
                    return (y.Size * y.Duplicats.Count).CompareTo(x.Size * x.Duplicats.Count);

                case DuplicateSortBy.Type:
                    return x.FileType.CompareTo(y.FileType);
                case DuplicateSortBy.DecType:
                    return y.FileType.CompareTo(x.FileType);

                default:
                    return 0;
            }
        }

        private void SortBy_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).ContextMenu.IsOpen = !((Button)sender).ContextMenu.IsOpen;
        }

        private void SortLists_Click(object sender, RoutedEventArgs e)
        {
            if (sender != ascrending_menu_item)
            {
                by_count_main_menu.IsChecked = false;
                by_type_main_menu.IsChecked = false;
                by_size_main_menu.IsChecked = false;
                by_total_size_main_menu.IsChecked = false;
                ((MenuItem)sender).IsChecked = true;
            }
            if (ascrending_menu_item.IsChecked)
            {
                if (sender == by_count_main_menu)
                    SortBy = DuplicateSortBy.Count;
                else if (sender == by_type_main_menu)
                    SortBy = DuplicateSortBy.Type;
                else if (sender == by_size_main_menu)
                    SortBy = DuplicateSortBy.Size;
                else if (sender == by_total_size_main_menu)
                    SortBy = DuplicateSortBy.TotalSize;
                else if (sender == ascrending_menu_item)
                    if ((int)SortBy < 5)
                        SortBy += 4;
            }
            else
            {
                if (sender == by_count_main_menu)
                    SortBy = DuplicateSortBy.DecCount;
                else if (sender == by_type_main_menu)
                    SortBy = DuplicateSortBy.DecType;
                else if (sender == by_size_main_menu)
                    SortBy = DuplicateSortBy.DecSize;
                else if (sender == by_total_size_main_menu)
                    SortBy = DuplicateSortBy.DecTotalSize;
                else if (sender == ascrending_menu_item)
                    if ((int)SortBy > 4)
                        SortBy -= 4;
            }
        }

        #endregion


        #region Operations

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            duplicate_size = 0;
            deleted_size = 0;
            total_size = 0;
            foreach (DuplicateFiles_ListView i in duplicate_data)
            {
                i.IsChecked = true;
                total_size += (i.Size * i.Duplicats.Count);
                duplicate_size += (i.Size * (i.Duplicats.Count - 1));
            }
            item_viewer.Items.Refresh();
            item_group_viewer.Items.Refresh();
            deleted_size = duplicate_size;
            duplicate.Width = (duplicate_size / total_size) * total.ActualWidth - 2;
            deleted_grid.Width = (deleted_size / total_size) * total.ActualWidth + 4;
            deleted_text.Text = Functions.FileSize(deleted_size, 2) + " will be deleted";
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (DuplicateFiles_ListView i in duplicate_data)
                i.IsChecked = false;
            item_group_viewer.Items.Refresh();
            item_viewer.Items.Refresh();
            deleted_grid.Width = 4;
            deleted_text.Text = "0 byte will be deleted";
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            List<string> files = new List<string>();
            foreach (var i in duplicate_data)
                foreach (var j in i.Duplicats)
                    if (j.IsChecked && File.Exists(j.File.FullName))
                        files.Add(j.File.FullName);

            if (files.Count > 0)
            {

                bool delete = false;
                if (files.Count > 1)
                {
                    delete = MessageBox.Show(string.Format("Are you sure you want to permanently delete these {0} items", files.Count), "Delete Files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
                }
                else
                {
                    delete = MessageBox.Show(string.Format("Are you sure you want to permanently delete '{0}'", files[0]), "Delete Files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
                }

                if (delete)
                {

                    BackgroundWorker deleter = new BackgroundWorker();
                    deleter.WorkerReportsProgress = true;

                    current_action.Text = "Deleting files...";
                    int progress = 0;
                    int errors = 0;
                    int total = files.Count;

                    deleter.DoWork += (object send, DoWorkEventArgs args) =>
                    {
                        Task delete_Task = new Task(() =>
                        {
                            foreach (string i in files)
                                if (File.Exists(i))
                                    try
                                    {
                                        FileSystem.DeleteFile(i, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                        progress += 1;
                                    }
                                    catch (Exception) {
                                        errors += 1;
                                    }
                                else
                                {
                                    errors += 1;
                                }
                        });
                        delete_Task.Start();
                        do
                        {
                            Thread.Sleep(50);
                            deleter.ReportProgress(0);
                        }
                        while (delete_Task.Status == TaskStatus.Running);
                    };

                    deleter.ProgressChanged += (object send, ProgressChangedEventArgs args) =>
                    {
                        double p = (Math.Round(1.0 * progress / total) * 100);
                        progress_text.Text = p.ToString("0.0%");
                        progressbar.Value = p / 100;
                    };

                    deleter.RunWorkerCompleted += (object send, RunWorkerCompletedEventArgs args) =>
                    {
                        for (int i = duplicate_data.Count - 1; i > -1; i--)
                        {
                            for (int j = duplicate_data[i].Duplicats.Count - 1; j > -1; j--)
                                if (duplicate_data[i].Duplicats[j].IsChecked && !File.Exists(duplicate_data[i].Duplicats[j].FullPath))
                                { duplicate_data[i].Duplicats.RemoveAt(j); duplicate_data[i].cheched_count--; }
                            if (duplicate_data[i].Duplicats.Count < 2)
                                duplicate_data.RemoveAt(i);
                            else
                                duplicate_data[i].FullPath = duplicate_data[i].Duplicats.Count + " duplicat" + (duplicate_data[i].Duplicats.Count == 1 ? "" : "s");
                        }
                        item_group_viewer.Items.Refresh();
                        item_viewer.Items.Refresh();
                        if(errors > 0)
                        {
                            MessageBox.Show(String.Format("Files deleted. {0} errors found.", errors), "", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Files deleted.", "", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    };

                    deleter.RunWorkerAsync();

                }
            }
            else
            {
                MessageBox.Show("You have not selected any file to delete.", "", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            tool_dock.Visibility = Visibility.Collapsed;
            flow_shart.Visibility = Visibility.Collapsed;
            log.IsEnabled = false;
            duplicate_data.Clear();
            item_viewer.DataContext = null;
            item_group_viewer.Items.Refresh();
        }

        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            if (finder.Log.Length > 0)
            {
                VistaSaveFileDialog sfd = new VistaSaveFileDialog();
                sfd.Title = "Select where to save the log:";
                sfd.AddExtension = true;
                sfd.Filter = "Log text file (*.log)|*.log";
                sfd.CheckPathExists = true;
                sfd.DefaultExt = ".log";
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        StreamWriter sw = new StreamWriter(sfd.OpenFile());
                        sw.Write(finder.Log);
                        sw.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Can't save the log\n" + ex.Message, "Save Log", MessageBoxButton.OK, MessageBoxImage.Hand);
                    }
                }
            }
        }


        private void ItemGroupViewer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (item_group_viewer.SelectedIndex > -1)
                item_viewer.DataContext = duplicate_data[item_group_viewer.SelectedIndex].Duplicats;
            item_viewer.Items.Refresh();
        }

        private void Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (item_viewer.SelectedIndex > -1)
                try { System.Diagnostics.Process.Start(duplicate_data[item_group_viewer.SelectedIndex].Duplicats[item_viewer.SelectedIndex].FullPath); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Something went wrong.", MessageBoxButton.OK, MessageBoxImage.Stop); }
        }

        private void Item_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                MessageBox.Show("Amazing feature is coming soon here! :)", "", MessageBoxButton.OK, MessageBoxImage.Information);
                //    if (File.Exists(dublicate_data[item_group_viewer.SelectedIndex].Dublicats[item_viewer.SelectedIndex].FullPath))
                //        new Preview().ShowDialog(new FileInfo(dublicate_data[item_group_viewer.SelectedIndex].Dublicats[item_viewer.SelectedIndex].FullPath), ItemType.File);
                //    if (MainWindow.GoToPath)
                //    {
                //        MainWindow.GoToPath = false;
                //        new MainWindow(MainWindow.to_go_path).ShowDialog();
                //    }
            }
            else if (e.Key == Key.Enter)
                Item_MouseDoubleClick(null, null);
            else if (e.Key == Key.Delete)
                Delete_Click(delete, null);
        }

        #endregion


        private void FindDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (duplicate_data.Count > 0)
                if (MessageBox.Show("You should clear list before continue, do you want to do that?", "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    ClearList_Click(null, null);

            if (duplicate_data.Count == 0)
            {
                Settings.FirstRun();
                Multi_Folders MF = new Multi_Folders();
                List<string> pathes = new List<string>();
                tool_dock.Visibility = Visibility.Collapsed;
                if (MF.ShowDialog(pathes) == true)
                {
                    progress_grid.Visibility = Visibility.Visible;
                    blur.Radius = 10;
                    List<Duplicate_Group> result = new List<Duplicate_Group>();
                    find_duplicate.IsEnabled = false;
                    log.IsEnabled = false;
                    //delete.IsEnabled = select_all.IsEnabled = select_none.IsEnabled = clear_list.IsEnabled = sort_button.IsEnabled = find_duplicate.IsEnabled = false;

                    BackgroundWorker bw = new BackgroundWorker();
                    bw.WorkerReportsProgress = true;

                    bw.DoWork += (object send, DoWorkEventArgs args) => 
                    {
                        //Thread.Sleep(500);
                        Thread op_thread = new Thread(() => 
                        {
                            result = finder.FindDuplicate(pathes,
                                Properties.Settings.Default.scan_quality,
                                Properties.Settings.Default.hidden_files,
                                Properties.Settings.Default.system_files,
                                Properties.Settings.Default.html_files,
                                Properties.Settings.Default.special_folder,
                                Properties.Settings.Default.special_folder_list.Replace("\r\n", "\n").Replace("\r", "\n").Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                );
                        });
                        op_thread.Start();
                        do
                        {
                            Thread.Sleep(50);
                            bw.ReportProgress(0);
                        }
                        while (op_thread.IsAlive);
                    };
                    
                    bw.ProgressChanged += (object send, ProgressChangedEventArgs args) => 
                    {
                        current_action.Text = finder.CurrentAction;
                        progress_text.Text = (Math.Round(finder.Persent, 1) / 100).ToString("0.0%");
                        progressbar.Value = finder.Persent / 100;
                    };

                    bw.RunWorkerCompleted += (object send, RunWorkerCompletedEventArgs args) =>
                    {
                        if (!finder.StopOperation)
                        {
                            foreach (var i in result)
                            {
                                var item = new DuplicateFiles_ListView();
                                item.Size = i.Size;
                                item.FileType = Functions.GetFileType(i.Items[0].File.Extension);
                                item.FullPath = i.Items.Count + (i.Items.Count == 1 ? " duplicat " : " duplicats");
                                item.TotalFileSize = Functions.FileSize(i.Size * i.Items.Count, 2);
                                duplicate_data.Add(item);
                                foreach (var j in i.Items)
                                    item.Duplicats.Add(new FilePath_ListView(j.File));
                            }
                        }
                        progressbar.Value = 100;
                        progress_text.Text = "100%";
                        find_duplicate.IsEnabled = true;
                        if (!finder.StopOperation)
                        {
                            MessageBox.Show(duplicate_data.Count > 0 ? string.Format("We found {0} item{1}", duplicate_data.Count, duplicate_data.Count > 1 ? "s" : "") : "Congratulations we did not find any duplicates here.", "Search done.", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (duplicate_data.Count > 0)
                            {
                                SelectAll_Click(null, null);
                                duplicate_data.Sort(Sorter);
                                progressbar.Value = 0;
                                flow_shart.Visibility = Visibility.Visible;
                                tool_dock.Visibility = Visibility.Visible;
                            }
                            log.IsEnabled = true;
                        }
                        blur.Radius = 1;
                        progress_grid.Visibility = Visibility.Hidden;
                        item_group_viewer.Items.Refresh();
                    };
                    bw.RunWorkerAsync();
                }
            }
        }

        private void CancelOperation_Click(object sender, RoutedEventArgs e)
        {
            if (finder.IsON)
                if (MessageBox.Show("Do you really want to STOP the operation?", "Stop Operation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    finder.CancelOperation();
        }


        #region Is Same Section

        Duplicate IsSame = new Duplicate(32768);

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog ofd = new VistaOpenFileDialog();
            ofd.Title = string.Format("Select the {0} file to compare:", sender == f_browse ? "first" : "second");
            ofd.CheckFileExists = true;
            if (ofd.ShowDialog() == true)
            {
                if (sender == f_browse)
                    FillFirstGrid(ofd.FileName);
                else
                    FillSecondGrid(ofd.FileName);
            }
        }

        private void FirstFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!starting_window)
            {
                go_button_color.Color = Colors.Tomato;
                if (File.Exists(f_filepath.Text))
                {
                    if (f_filepath.Text.Equals(s_filepath.Text))
                    {
                        f_filepath.Text = "-";
                        return;
                    }
                    FillFirstGrid(f_filepath.Text);
                    if (File.Exists(s_filepath.Text))
                        go_button_color.Color = Colors.ForestGreen;
                }
                else
                    RestValue(f_grid);
            }
        }

        private void SecondFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!starting_window)
            {
                go_button_color.Color = Colors.Tomato;
                if (File.Exists(s_filepath.Text))
                {
                    if (f_filepath.Text.Equals(s_filepath.Text))
                    {
                        s_filepath.Text = "-";
                        return;
                    }
                    FillSecondGrid(s_filepath.Text);
                    if (File.Exists(f_filepath.Text))
                        go_button_color.Color = Colors.ForestGreen;
                }
                else
                    RestValue(s_grid);
            }
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            string file_name = "-";
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                file_name = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            else if (e.Data.GetDataPresent(DataFormats.Text))
                file_name = (string)e.Data.GetData(DataFormats.Text);
            if (file_name != "-" && File.Exists(file_name))
            {
                if (sender == f_grid)
                    FillFirstGrid(file_name);
                else
                    FillSecondGrid(file_name);
            }
        }

        private void FillFirstGrid(string p)
        {
            if (p != s_filepath.Text)
            {
                try
                {
                    FileInfo file = new FileInfo(p);
                    f_filename.Text = file.Name;
                    f_filepath.Text = file.FullName;
                    f_date.Text = "Date: " + file.LastWriteTimeUtc.ToString("yyyy/MM/dd HH:mm:ss");
                    //f_date.Text = "Date: " + string.Format("{0}/{1}/{2}  ", OrginizeDate(file.LastWriteTimeUtc.Day, 2), OrginizeDate(file.LastWriteTimeUtc.Month, 2), OrginizeDate(file.LastWriteTimeUtc.Year, 4)) + string.Format("{0}:{1}", OrginizeDate(file.LastWriteTimeUtc.Hour, 2), OrginizeDate(file.LastWriteTimeUtc.Minute, 2));
                    f_image.Source = Functions.GetFileImage256(file.Extension);
                    f_size.Text = "Size: " + Functions.FileSize(file.Length, 2);
                    f_type.Text = "Type: " + Functions.GetFileType(file.Extension) + (file.Extension.Length > 0 ? string.Format(" ({0})", file.Extension) : string.Empty);
                }
                catch (Exception ex)
                {
                    RestValue(f_grid);
                    f_filepath.Text = "-";
                    s_filepath.Text = "-";
                    MessageBox.Show("Something went wrong.\n" + ex.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
                MessageBox.Show("The two files can't be the same file.", "", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void FillSecondGrid(string p)
        {
            if (p != f_filepath.Text)
            {
                try
                {
                    FileInfo file = new FileInfo(p);
                    s_filename.Text = file.Name;
                    s_filepath.Text = file.FullName;
                    s_date.Text = "Date: " + file.LastWriteTimeUtc.ToString("yyyy/MM/dd HH:mm:ss");
                    //s_date.Text = "Date: " + string.Format("{0}/{1}/{2}  ", OrginizeDate(file.LastWriteTimeUtc.Day, 2), OrginizeDate(file.LastWriteTimeUtc.Month, 2), OrginizeDate(file.LastWriteTimeUtc.Year, 4)) + string.Format("{0}:{1}", OrginizeDate(file.LastWriteTimeUtc.Hour, 2), OrginizeDate(file.LastWriteTimeUtc.Minute, 2));
                    s_image.Source = Functions.GetFileImage256(file.Extension);
                    s_size.Text = "Size: " + Functions.FileSize(file.Length, 2);
                    s_type.Text = "Type: " + Functions.GetFileType(file.Extension) + (file.Extension.Length > 0 ? string.Format(" ({0})", file.Extension) : string.Empty);
                }
                catch (Exception ex)
                {
                    RestValue(s_grid);
                    f_filepath.Text = "-";
                    s_filepath.Text = "-";
                    MessageBox.Show("Something went wrong.\n" + ex.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
                MessageBox.Show("The two files can't be the same file.", "", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void RestValue(Grid grid)
        {
            if (grid == f_grid)
            {
                f_filename.Text = "First file";
                f_date.Text = "-";
                f_image.Source = new BitmapImage(new Uri("Assets/Icons/warning-grey.png", UriKind.Relative));
                f_size.Text = "-";
                f_type.Text = "-";
            }
            else if (grid == s_grid)
            {
                s_filename.Text = "Second file";
                s_date.Text = "-";
                s_image.Source = new BitmapImage(new Uri("Assets/Icons/warning-grey.png", UriKind.Relative));
                s_size.Text = "-";
                s_type.Text = "-";
            }
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(f_filepath.Text))
            {
                if (File.Exists(s_filepath.Text))
                {
                    progress_grid.Visibility = Visibility.Visible;
                    blur.Radius = 10;
                    FileInfo first = new FileInfo(s_filepath.Text);
                    FileInfo second = new FileInfo(f_filepath.Text);
                    IsSameResult result = IsSameResult.Unknown;
                    //log.IsEnabled = false;
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.WorkerReportsProgress = true;
                    
                    bw.DoWork += (object send, DoWorkEventArgs args) =>
                    {
                        Thread op_thread = new Thread(() =>
                        {
                            result = IsSame.IsSame(first, second, Properties.Settings.Default.scan_quality);
                        });
                        op_thread.Start();
                        do
                        {
                            Thread.Sleep(50);
                            bw.ReportProgress(0);
                        }
                        while (op_thread.IsAlive);
                    };

                    bw.ProgressChanged += (object send, ProgressChangedEventArgs args) =>
                    {
                        current_action.Text = IsSame.CurrentAction;
                        progress_text.Text = (Math.Round(IsSame.Persent, 1) / 100).ToString("0.0%");
                        progressbar.Value = IsSame.Persent / 100;
                    };

                    bw.RunWorkerCompleted += (object send, RunWorkerCompletedEventArgs args) =>
                    {
                        progressbar.Value = 100;
                        progress_text.Text = "100%";
                        if (!IsSame.StopOperation)
                        {
                            if (result == IsSameResult.Same)
                            {
                                MessageBox.Show("Files are equal.", "", MessageBoxButton.OK, MessageBoxImage.Information);
                                if (!first.Extension.Equals(second.Extension, StringComparison.OrdinalIgnoreCase))
                                        //if (MessageBox.Show("Files seem have deferent extention, do you want to run 'Super extention changer' to detemenate what is the correct extention?", "Something is wrong.", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                        MessageBox.Show("Amazing feature is coming soon here! ;)", "", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else if (result == IsSameResult.NotSame)
                                MessageBox.Show("Files are not equal.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                            else if (result != IsSameResult.Canceled)
                                MessageBox.Show("Something went wrong.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                                //log.IsEnabled = true;
                            }
                        progressbar.Value = 0;
                        progress_grid.Visibility = Visibility.Collapsed;
                        blur.Radius = 1;
                    };

                    bw.RunWorkerAsync();
                }
                else if (s_filepath.Text != "-" && s_filepath.Text != "")
                    MessageBox.Show(string.Format("The file '{0}' does not exist.", s_filepath.Text), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (f_filepath.Text != "-" && f_filepath.Text != "")
                MessageBox.Show(string.Format("The file '{0}' does not exist.", f_filepath.Text), "", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        

        #endregion

    }

    class DuplicateFileInfo
    {
        internal string fullpath;
        internal long size;
        internal FileInfo file;

        internal string SHA1;
        internal string parts;

        public DuplicateFileInfo() { }

        public DuplicateFileInfo(FileInfo item)
        {
            file = item;
            fullpath = file.FullName;
            size = file.Length;
        }

        public int CompateTo(DuplicateFileInfo x)
        {
            if (x == null) return -1;
            if (x.parts.SequenceEqual(parts))
                return 0;
            else return -1;
        }
    }

    class BasicFileInfo : IComparable
    {
        public string name;
        public string extention;
        public string fullpath;
        public long size;
        public string SHA1;

        public BasicFileInfo(FileInfo file)
        {
            name = file.Name;
            extention = file.Extension;
            fullpath = file.FullName;
            size = file.Length;
        }

        public int CompareTo(object obj)
        {
            long s = ((BasicFileInfo)obj).size;
            if (s > this.size)
                return 1;
            else if (s < this.size)
                return -1;
            else
                return 0;
        }

        internal void SetHash(byte[] p)
        {
            StringBuilder h = new StringBuilder();
            foreach (byte i in p)
                h.Append(i.ToString());
            SHA1 = h.ToString();
        }
    }

    class DuplicateFiles_ListView : ListItem
    {
        internal int cheched_count = 0;
        long size = 0;

        public long Size
        {
            get => size;
            set
            {
                FileSize = Functions.FileSize(value, 2);
                size = value;
            }
        }

        public DuplicateFiles_ListView()
        {
            Duplicats = new List<FilePath_ListView>();
            Image = Functions.GetFileImage24("directory");
        }

        public DuplicateFiles_ListView(string file_type, long size, Point Tag)
        {
            Image = Functions.GetFileImage24("directory");
            FileType = file_type;
            this.size = size;
            FileSize = Functions.FileSize(size, 2);
            Duplicats = new List<FilePath_ListView>();
            Point = Tag;
        }

        private bool? is_cheched;
        public Boolean? IsChecked
        {
            get { return is_cheched; }
            set
            {
                if (value.HasValue)
                {
                    foreach (var i in Duplicats)
                        i.IsChecked = value.Value;
                    if (value.Value)
                        Duplicats[0].IsChecked = false;
                    cheched_count = Duplicats.Count - 1;
                }
                if (value.Value)
                    is_cheched = null;
                else
                    is_cheched = value;
                //refresh list view;
            }
        }

        public void ChangeIsCheakedValue(bool? value)
        {
            is_cheched = value;
        }

        List<FilePath_ListView> duplicats;
        public List<FilePath_ListView> Duplicats { get => duplicats; set => duplicats = value; }

        string file_type;
        public string FileType { get => file_type; set => file_type = value; }


        string total_file_size;
        public string TotalFileSize { get => total_file_size; set => total_file_size = value; }

        string file_size;
        public string FileSize { get => file_size; set => file_size = value; }

        Point point;
        public Point Point { get => point; set => point = value; }
    }

    class FilePath_ListView : ListItem
    {
        public FilePath_ListView(FileInfo item)
        {
            FullPath = item.FullName;
            File = item;
            IsChecked = false;
            Image = Functions.GetFileImage24(item.Extension);
        }

        bool is_checked;
        public bool IsChecked
        {
            get { return is_checked; }
            set { is_checked = value; }
        }
    }

    enum DuplicateFileRemoverPages
    {
        DuplicateFind = 1,
        IsSimeller = 2,
        WhatsDeferent = 3
    }

    enum DuplicateSortBy
    {
        Count = 1,
        Type = 2,
        Size = 3,
        TotalSize = 4,
        DecCount = 5,
        DecType = 6,
        DecSize = 7,
        DecTotalSize = 8
    }
}


//private void FindDuplicate(List<string> _paths)
//{
//    List<string> paths = new List<string>();
//    foreach (string i in _paths)
//        paths.Add(i);
//    progressbar.Visibility = Visibility.Visible;
//    main_grid.IsEnabled = false;
//    TaskbarItemInfo.ProgressValue = 0;

//    BackgroundWorker find_duplicate = new BackgroundWorker();
//    find_duplicate.WorkerReportsProgress = true;
//    find_duplicate.DoWork += (object dw_sender, DoWorkEventArgs dw_e) =>
//    {
//        bool first_stage = true;
//        Thread progress_thread = new Thread(() =>
//        {
//            for (int i = 0; i < 126 && first_stage; i++)
//            {
//                if (CloseWindow) return;
//                if (!find_duplicate.IsBusy)
//                    break;
//                else
//                    try
//                    { if (first_stage) find_duplicate.ReportProgress(0, 0.75); }
//                    catch (Exception) { }
//                Thread.Sleep(100);
//                int sleep_time = i < 100 ? 100 : i < 120 ? 1000 : i < 126 ? 5000 : 10000;
//            }
//        });
//        List<DublicateFileInfo> files = new List<DublicateFileInfo>();
//        foreach (string i in paths)
//        {
//            if (File.Exists(i))
//            {
//                FileInfo tmp_file = new FileInfo(i);
//                files.Add(new DublicateFileInfo() { fullpath = tmp_file.FullName, size = tmp_file.Length, file = tmp_file });
//            }
//            else
//            {
//                try
//                { GetAllFiles(files, new DirectoryInfo(i)); }
//                catch (Exception) { }
//            }
//        }
//        first_stage = false;
//        find_duplicate.ReportProgress(-1, 25);
//        files.Sort((x, y) => x.size.CompareTo(y.size));
//        int start_i = 0, start_k = 0;

//        for (int i = 0; i < files.Count - 1; i++)
//        {
//            find_duplicate.ReportProgress(0, 70.0 / files.Count);
//            start_i = i;
//            int same_size = 0;
//            if (files.Count > i + 1 && files[i].size == files[i + 1].size)
//            {
//                ReadPartsOF(files[i]);
//                same_size++;
//            }
//            else
//                continue;
//            int j;
//            for (j = i + 1; j < files.Count; j++)
//            {
//                if (files[i].size == files[j].size)
//                {
//                    ReadPartsOF(files[j]);
//                    same_size++;
//                }
//                else break;
//            }
//            if (same_size > 1)
//            {
//                List<DublicateFileInfo> small_list = files.GetRange(start_i, same_size);
//                small_list.Sort((x, y) => x.parts.CompareTo(y.parts));
//                for (int k = 0; k < small_list.Count - 1; k++)
//                {
//                    start_k = k;
//                    int duplicates = 0;
//                    if (small_list.Count > k + 1 && small_list[k].parts == small_list[k + 1].parts)
//                    {
//                        CalcHash(small_list[k]);
//                        duplicates++;
//                    }
//                    else
//                        continue;
//                    int l;
//                    for (l = k + 1; l < small_list.Count; l++)
//                    {
//                        if (small_list[k].parts.Equals(small_list[l].parts))
//                        {
//                            CalcHash(small_list[l]);
//                            duplicates++;
//                        }
//                        else break;
//                    }
//                    if (duplicates > 1)
//                    {
//                        List<DublicateFileInfo> micro_list = small_list.GetRange(start_k, duplicates);
//                        micro_list.Sort((x, y) =>/* x.SHA1 == null ? -1 : y.SHA1 == null ? 1 : */x.SHA1.CompareTo(y.SHA1));
//                        for (int m = 0; m < micro_list.Count - 1; m++)
//                        {
//                            if (micro_list.Count > m + 1 && micro_list[m].SHA1 == micro_list[m + 1].SHA1)
//                            {
//                                find_duplicate.ReportProgress(1);
//                                find_duplicate.ReportProgress(2, micro_list[m].file);
//                            }
//                            else
//                                continue;
//                            int n;
//                            for (n = m + 1; n < micro_list.Count; n++)
//                                if (micro_list[m].SHA1.Equals(micro_list[n].SHA1))
//                                    find_duplicate.ReportProgress(2, micro_list[n].file);
//                                else
//                                    break;
//                            m = n - 1;
//                            find_duplicate.ReportProgress(3);
//                        }
//                    }
//                    k = l - 1;// will be l in for loop
//                    duplicates = 0;
//                    //break;
//                }
//            }
//            i = j - 1;
//            same_size = 0;
//            //break;
//        }
//    };

//    find_duplicate.ProgressChanged += (object bw_sender, ProgressChangedEventArgs bw_e) =>
//    {
//        switch (bw_e.ProgressPercentage)
//        {
//            case -1:
//                progressbar.Value = Convert.ToDouble(bw_e.UserState);
//                TaskbarItemInfo.ProgressValue = progressbar.Value / 100;
//                break;
//            case 0:
//                progressbar.Value += Convert.ToDouble(bw_e.UserState);
//                TaskbarItemInfo.ProgressValue = progressbar.Value / 100;
//                break;
//            case 1:
//                dublicate_data.Add(new DublicateFiles_ListView());
//                item_group_viewer.Items.Refresh();
//                break;
//            case 2:
//                dublicate_data[dublicate_data.Count - 1].Duplicats.Add(new FilePath_ListView(((FileInfo)bw_e.UserState)));
//                break;
//            case 3:
//                dublicate_data[dublicate_data.Count - 1].Size = dublicate_data[dublicate_data.Count - 1].Duplicats[0].File.Length;
//                dublicate_data[dublicate_data.Count - 1].FileType = Functions.GetFileType(dublicate_data[dublicate_data.Count - 1].Duplicats[0].File.Extension);
//                dublicate_data[dublicate_data.Count - 1].FullPath = dublicate_data[dublicate_data.Count - 1].Duplicats.Count + (dublicate_data[dublicate_data.Count - 1].Duplicats.Count > 1 ? " dublicats " : " dublicat");
//                dublicate_data[dublicate_data.Count - 1].TotalFileSize = Functions.FileSize(dublicate_data[dublicate_data.Count - 1].Size * dublicate_data[dublicate_data.Count - 1].Duplicats.Count, 2);
//                break;
//        }
//    };

//    find_duplicate.RunWorkerCompleted += (object bw_sender, RunWorkerCompletedEventArgs bw_e) =>
//    {
//        if (!CloseWindow)
//        {
//            main_grid.IsEnabled = true;
//            SelectAll_Click(null, null);
//            if (dublicate_data.Count > 0)
//                delete.IsEnabled = select_all.IsEnabled = select_none.IsEnabled = clear_list.IsEnabled = sort_button.IsEnabled = true;
//            find_dublicate.IsEnabled = true;
//            items_label.Text = dublicate_data.Count > 1 ? "items" : "item";
//            number.Text = " " + dublicate_data.Count;
//            dublicate_data.Sort(Sorter);
//            item_group_viewer.Items.Refresh();
//            progressbar.Value = 100;
//            TaskbarItemInfo.ProgressValue = 100;
//            MessageBox.Show(dublicate_data.Count > 0 ? string.Format("We found {0} item{1}", dublicate_data.Count, dublicate_data.Count > 1 ? "s" : "") : "Congratulations we did not find any dublicates here.", "Search done.", MessageBoxButton.OK, MessageBoxImage.Information);
//            TaskbarItemInfo.ProgressValue = 0;
//            progressbar.Visibility = Visibility.Hidden;
//            flow_shart.Visibility = Visibility.Visible;
//        }
//    };
//    find_duplicate.RunWorkerAsync();
//}
