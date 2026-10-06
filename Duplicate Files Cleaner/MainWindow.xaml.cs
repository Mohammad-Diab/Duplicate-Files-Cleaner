using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;

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
        Duplicate IsSame = new Duplicate(32768);

        bool starting_window = true;

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
            if (operation_running)
            {
                if (MessageBox.Show("An operation is running. Stop it and close?", "Stop Operation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    cancel_operation?.Invoke();
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
            new Settings() { Owner = this }.ShowDialog();
        }

        private void AboutTool_Click(object sender, RoutedEventArgs e)
        {
            new About() { Owner = this }.ShowDialog();
        }

        #endregion


        #region Background operations with progress

        bool operation_running;
        Action cancel_operation;
        DispatcherTimer progress_timer;

        /// <summary>
        /// Runs work on a background thread and shows the progress panel until it ends. done runs on the UI thread.
        /// </summary>
        void RunWithProgress(Func<double> percent, Func<bool> indeterminate, Func<string> action, Action cancel, Action work, Action<Exception> done)
        {
            operation_running = true;
            cancel_operation = cancel;
            progress_grid.Visibility = Visibility.Visible;
            blur.Radius = 10;
            current_action.Text = action();
            UpdateProgress(0, true);

            progress_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            progress_timer.Tick += (s, a) =>
            {
                current_action.Text = action();
                UpdateProgress(percent(), indeterminate());
            };
            progress_timer.Start();

            var thread = new Thread(() =>
            {
                Exception error = null;
                try { work(); }
                catch (Exception ex) { error = ex; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    progress_timer.Stop();
                    UpdateProgress(100, false);
                    operation_running = false;
                    cancel_operation = null;
                    progress_grid.Visibility = Visibility.Collapsed;
                    blur.Radius = 1;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    done(error);
                }));
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);   // the Recycle Bin uses the Windows shell
            thread.Start();
        }

        void UpdateProgress(double percent, bool indeterminate)
        {
            progressbar.IsIndeterminate = indeterminate;
            progressbar.Value = percent / 100;
            progress_text.Text = indeterminate ? "..." : (percent / 100).ToString("0.0%");
            TaskbarItemInfo.ProgressState = indeterminate ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
            TaskbarItemInfo.ProgressValue = percent / 100;
        }

        private void CancelOperation_Click(object sender, RoutedEventArgs e)
        {
            if (operation_running && cancel_operation != null)
                if (MessageBox.Show("Do you really want to stop the operation?", "Stop Operation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    cancel_operation?.Invoke();
        }

        #endregion


        #region Selection

        private void Group_Click(object sender, RoutedEventArgs e)
        {
            var group = (DuplicateFiles_ListView)((CheckBox)sender).DataContext;
            if (group.Duplicats.Any(d => d.IsChecked))
                group.SelectNone();
            else
                group.SelectAllButKeeper();
            RefreshSelection();
        }

        private void Sun_Click(object sender, RoutedEventArgs e)
        {
            var box = (CheckBox)sender;
            var file = (FilePath_ListView)box.DataContext;
            var group = duplicate_data.FirstOrDefault(g => g.Duplicats.Contains(file));
            if (group == null) return;
            if (file.IsChecked && group.Duplicats.All(d => d.IsChecked))
                if (MessageBox.Show("You are about to remove all copies of this file. Do you want to do that?", "Duplicate Files Cleaner", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                    file.IsChecked = false;
            RefreshSelection();
        }

        /// <summary>Redraws both lists and the size bar from the current check boxes.</summary>
        void RefreshSelection()
        {
            double total_size = 0, duplicate_size = 0, deleted_size = 0;
            foreach (var g in duplicate_data)
            {
                total_size += g.Size * g.Duplicats.Count;
                duplicate_size += g.Size * Math.Max(0, g.Duplicats.Count - 1);
                deleted_size += g.Size * g.Duplicats.Count(d => d.IsChecked);
            }
            double width = total.ActualWidth;
            duplicate.Width = total_size > 0 ? Math.Max(0, duplicate_size / total_size * width - 2) : 0;
            deleted_grid.Width = total_size > 0 ? deleted_size / total_size * width + 4 : 4;
            deleted_text.Text = Functions.FileSize(deleted_size, 2) + " will be deleted";
            item_group_viewer.Items.Refresh();
            item_viewer.Items.Refresh();
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
                    if ((int)SortBy > 4)
                        SortBy -= 4;
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
                    if ((int)SortBy < 5)
                        SortBy += 4;
            }
        }

        #endregion


        #region Operations

        /// <summary>Selects every copy except the one the "Copy to keep" rules pick in each group.</summary>
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (DuplicateFiles_ListView i in duplicate_data)
                i.SelectAllButKeeper();
            RefreshSelection();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (DuplicateFiles_ListView i in duplicate_data)
                i.SelectNone();
            RefreshSelection();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!results_confirmed)
            {
                if (MessageBox.Show("These results come from a preview scan and may include files that are not duplicates.\n\nCheck them byte by byte now? Only the real duplicates will stay in the list.", "Preview results", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ConfirmPreview();
                return;
            }

            var targets = duplicate_data.SelectMany(g => g.Duplicats.Where(d => d.IsChecked)).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("You have not selected any file to delete.", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool recycle = Properties.Settings.Default.recycle_bin;
            string size = Functions.FileSize(targets.Sum(t => (double)t.ScanLength), 2);
            string what = targets.Count == 1 ? "'" + targets[0].FullPath + "'" : string.Format("these {0} files", targets.Count);
            string message = recycle
                ? string.Format("Move {0} ({1}) to the Recycle Bin?", what, size)
                : string.Format("Permanently delete {0} ({1})?\n\nThis can't be undone.", what, size);
            int all_copies = duplicate_data.Count(g => g.Duplicats.Count > 0 && g.Duplicats.All(d => d.IsChecked));
            if (all_copies > 0)
                message += string.Format("\n\nWarning: in {0} group{1}, all copies are selected, so no copy will be left.", all_copies, all_copies == 1 ? "" : "s");
            if (MessageBox.Show(message, recycle ? "Move to Recycle Bin" : "Delete Files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            int done = 0, changed = 0, missing = 0, no_bin = 0;
            var errors = new List<string>();
            bool stop = false;

            RunWithProgress(
                () => 100.0 * (done + changed + missing + no_bin + errors.Count) / targets.Count,
                () => false,
                () => recycle ? "Moving files to the Recycle Bin..." : "Deleting files...",
                () => stop = true,
                () =>
                {
                    foreach (var t in targets)
                    {
                        if (stop) break;
                        var file = new FileInfo(t.FullPath);
                        try
                        {
                            if (!file.Exists) { missing++; continue; }
                            // Never delete a file that changed after the scan: it may not be a duplicate any more.
                            if (file.Length != t.ScanLength || file.LastWriteTimeUtc != t.ScanWriteTimeUtc) { changed++; continue; }
                            if (recycle)
                            {
                                // USB and network drives have no Recycle Bin: Windows would delete the file for good.
                                if (!Functions.HasRecycleBin(file.FullName)) { no_bin++; continue; }
                                FileSystem.DeleteFile(file.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                            }
                            else
                                file.Delete();
                            done++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(t.FullPath + "  (" + ex.Message + ")");
                        }
                    }
                },
                error =>
                {
                    foreach (var g in duplicate_data)
                        g.Duplicats.RemoveAll(d => d.IsChecked && !File.Exists(d.FullPath));
                    duplicate_data.RemoveAll(g => g.Duplicats.Count < 2);
                    foreach (var g in duplicate_data)
                        g.UpdateTitle();
                    if (duplicate_data.Count == 0)
                        ClearList_Click(null, null);
                    RefreshSelection();

                    var report = new List<string>();
                    report.Add(string.Format(recycle ? "{0} file{1} moved to the Recycle Bin." : "{0} file{1} deleted.", done, done == 1 ? "" : "s"));
                    if (changed > 0) report.Add(string.Format("{0} skipped because they changed after the scan.", changed));
                    if (missing > 0) report.Add(string.Format("{0} were already gone.", missing));
                    if (no_bin > 0) report.Add(string.Format("{0} skipped: they are on a USB or network drive, which has no Recycle Bin. Choose \"Delete permanently\" in Settings to remove them.", no_bin));
                    if (errors.Count > 0) report.Add(string.Format("{0} could not be deleted:\n{1}", errors.Count, string.Join("\n", errors.Take(10).ToArray()) + (errors.Count > 10 ? "\n..." : "")));
                    if (error != null) report.Add("Error: " + error.Message);
                    bool problem = changed + missing + no_bin + errors.Count > 0 || error != null;
                    MessageBox.Show(string.Join("\n\n", report.ToArray()), "Duplicate Files Cleaner", MessageBoxButton.OK, problem ? MessageBoxImage.Warning : MessageBoxImage.Information);
                });
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
            string text = last_log;
            if (text.Length > 0)
            {
                VistaSaveFileDialog sfd = new VistaSaveFileDialog();
                sfd.Title = "Save the log";
                sfd.FileName = "Duplicate Files Cleaner " + DateTime.Now.ToString("yyyy-MM-dd HH-mm") + ".log";
                sfd.AddExtension = true;
                sfd.Filter = "Log file (*.log)|*.log|Text file (*.txt)|*.txt";
                sfd.CheckPathExists = true;
                sfd.DefaultExt = ".log";
                if (sfd.ShowDialog(this) == true)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, text, System.Text.Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Can't save the log.\n" + ex.Message, "Save Log", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        string last_log = "";

        private void ItemGroupViewer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (item_group_viewer.SelectedIndex > -1)
                item_viewer.DataContext = duplicate_data[item_group_viewer.SelectedIndex].Duplicats;
            item_viewer.Items.Refresh();
        }

        private void Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (item_viewer.SelectedIndex > -1 && item_group_viewer.SelectedIndex > -1)
                try { System.Diagnostics.Process.Start(duplicate_data[item_group_viewer.SelectedIndex].Duplicats[item_viewer.SelectedIndex].FullPath); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Can't open the file", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Item_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Item_MouseDoubleClick(null, null);
            else if (e.Key == Key.Delete)
                Delete_Click(delete, null);
        }

        #endregion


        private void FindDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (duplicate_data.Count > 0)
                if (MessageBox.Show("The current results will be cleared. Continue?", "Duplicate Files Cleaner", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    ClearList_Click(null, null);

            if (duplicate_data.Count > 0)
                return;

            Settings.FirstRun();
            Multi_Folders MF = new Multi_Folders() { Owner = this };
            List<string> paths = new List<string>();
            tool_dock.Visibility = Visibility.Collapsed;
            if (MF.ShowDialog(paths) != true)
                return;

            var s = Properties.Settings.Default;
            var options = new ScanOptions
            {
                Mode = Functions.ScanModeFromSettings(),
                SkipHidden = s.hidden_files,
                SkipSystem = s.system_files,
                SkipGenerated = s.skip_generated,
                GeneratedFolderNames = s.generated_folders.Split(';').Select(n => n.Trim()).Where(n => n.Length > 0).ToList(),
                ExcludedFolders = s.special_folder ? Functions.SplitLines(s.special_folder_list) : new List<string>()
            };
            finder.BufferSize = s.buffer_size;
            find_duplicate.IsEnabled = false;
            log.IsEnabled = false;
            List<Duplicate_Group> result = new List<Duplicate_Group>();

            RunWithProgress(
                () => finder.Persent,
                () => finder.Indeterminate,
                () => finder.CurrentAction,
                () => finder.CancelOperation(),
                () => result = finder.FindDuplicate(paths, options),
                error =>
                {
                    find_duplicate.IsEnabled = true;
                    last_log = finder.Log;
                    log.IsEnabled = last_log.Length > 0;
                    if (error != null)
                    {
                        MessageBox.Show("The search stopped because of an error.\n" + error.Message, "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (finder.StopOperation)
                        return;

                    results_confirmed = !options.IsPreview;
                    ShowResults(result);
                    if (duplicate_data.Count == 0)
                        MessageBox.Show("No duplicate files were found.", "Search done", MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (results_confirmed)
                        MessageBox.Show(string.Format("Found {0} group{1} of duplicate files.", duplicate_data.Count, duplicate_data.Count > 1 ? "s" : ""), "Search done", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show(string.Format("Preview: found {0} group{1} of possible duplicates.\n\nThese results are not confirmed and may include files that are not duplicates, so nothing is selected. Press Delete to confirm them with an exact check first.", duplicate_data.Count, duplicate_data.Count > 1 ? "s" : ""), "Search done (preview)", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
        }

        List<Duplicate_Group> last_groups = new List<Duplicate_Group>();
        bool results_confirmed = true;

        void ShowResults(List<Duplicate_Group> groups)
        {
            last_groups = groups;
            duplicate_data.Clear();
            item_viewer.DataContext = null;
            foreach (var g in groups)
            {
                var item = new DuplicateFiles_ListView();
                item.Size = g.Size;
                item.FileType = Functions.GetFileType(g.Items[0].File.Extension);
                foreach (var j in g.Items)
                    item.Duplicats.Add(new FilePath_ListView(j.File));
                item.UpdateTitle();
                duplicate_data.Add(item);
            }
            duplicate_data.Sort(Sorter);
            bool any = duplicate_data.Count > 0;
            flow_shart.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            tool_dock.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            UpdateLayout();
            if (any && results_confirmed)
                SelectAll_Click(null, null);
            else
                RefreshSelection();
            if (any && !results_confirmed)
                deleted_text.Text = "Preview - not confirmed";
        }

        /// <summary>Runs the exact check on preview results and keeps only the real duplicates.</summary>
        void ConfirmPreview()
        {
            List<Duplicate_Group> confirmed = null;
            int before = last_groups.Count;
            var groups = last_groups;
            RunWithProgress(
                () => finder.Persent,
                () => finder.Indeterminate,
                () => finder.CurrentAction,
                () => finder.CancelOperation(),
                () => confirmed = finder.Confirm(groups),
                error =>
                {
                    last_log = finder.Log;
                    if (error != null || confirmed == null)
                    {
                        if (error != null)
                            MessageBox.Show("The check stopped because of an error.\n" + error.Message, "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    results_confirmed = true;
                    ShowResults(confirmed);
                    int removed_files = groups.Sum(g => g.Items.Count) - confirmed.Sum(g => g.Items.Count);
                    MessageBox.Show(string.Format("{0} group{1} confirmed as real duplicates.{2}\n\nThe copies to delete are now selected. Press Delete again to delete them.",
                        confirmed.Count, confirmed.Count == 1 ? "" : "s",
                        removed_files > 0 ? string.Format("\n{0} file{1} turned out not to be duplicates and were removed from the list.", removed_files, removed_files == 1 ? "" : "s") : ""),
                        "Exact check done", MessageBoxButton.OK, MessageBoxImage.Information);
                });
        }


        #region Is Same Section

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog ofd = new VistaOpenFileDialog();
            ofd.Title = string.Format("Select the {0} file to compare", sender == f_browse ? "first" : "second");
            ofd.CheckFileExists = true;
            if (ofd.ShowDialog(this) == true)
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
                    f_date.Text = "Date: " + file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
                    f_image.Source = Functions.GetFileImage256(file.Extension);
                    f_size.Text = "Size: " + Functions.FileSize(file.Length, 2);
                    f_type.Text = "Type: " + Functions.GetFileType(file.Extension) + (file.Extension.Length > 0 ? string.Format(" ({0})", file.Extension) : string.Empty);
                }
                catch (Exception ex)
                {
                    RestValue(f_grid);
                    f_filepath.Text = "-";
                    MessageBox.Show("Can't read the file.\n" + ex.Message, "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
                MessageBox.Show("The two files can't be the same file.", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    s_date.Text = "Date: " + file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
                    s_image.Source = Functions.GetFileImage256(file.Extension);
                    s_size.Text = "Size: " + Functions.FileSize(file.Length, 2);
                    s_type.Text = "Type: " + Functions.GetFileType(file.Extension) + (file.Extension.Length > 0 ? string.Format(" ({0})", file.Extension) : string.Empty);
                }
                catch (Exception ex)
                {
                    RestValue(s_grid);
                    s_filepath.Text = "-";
                    MessageBox.Show("Can't read the file.\n" + ex.Message, "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
                MessageBox.Show("The two files can't be the same file.", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!File.Exists(f_filepath.Text))
            {
                MessageBox.Show(f_filepath.Text != "-" && f_filepath.Text != "" ? string.Format("The file '{0}' does not exist.", f_filepath.Text) : "Choose the first file.", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!File.Exists(s_filepath.Text))
            {
                MessageBox.Show(s_filepath.Text != "-" && s_filepath.Text != "" ? string.Format("The file '{0}' does not exist.", s_filepath.Text) : "Choose the second file.", "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            FileInfo first = new FileInfo(f_filepath.Text);
            FileInfo second = new FileInfo(s_filepath.Text);
            var mode = Functions.ScanModeFromSettings();
            IsSame.BufferSize = Properties.Settings.Default.buffer_size;
            IsSameResult result = IsSameResult.Unknown;

            RunWithProgress(
                () => IsSame.Persent,
                () => IsSame.Indeterminate,
                () => IsSame.CurrentAction,
                () => IsSame.CancelOperation(),
                () => result = IsSame.IsSame(first, second, mode),
                error =>
                {
                    if (error != null)
                        MessageBox.Show("The comparison stopped because of an error.\n" + error.Message, "Duplicate Files Cleaner", MessageBoxButton.OK, MessageBoxImage.Error);
                    else if (result == IsSameResult.Same)
                        MessageBox.Show(mode == ScanMode.SizeOnly || mode == ScanMode.Sample
                            ? "The files look identical (checked with the \"" + (mode == ScanMode.SizeOnly ? "size only" : "fast") + "\" scan mode)."
                            : "The files are identical.", "Compare", MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (result == IsSameResult.NotSame)
                        MessageBox.Show("The files are different.", "Compare", MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (result == IsSameResult.FileIsNotExist)
                        MessageBox.Show("One of the files was not found.", "Compare", MessageBoxButton.OK, MessageBoxImage.Error);
                    else if (result == IsSameResult.CanNotReadFile)
                        MessageBox.Show("One of the files could not be read.", "Compare", MessageBoxButton.OK, MessageBoxImage.Error);
                });
        }

        #endregion

    }

    class DuplicateFiles_ListView : ListItem
    {
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

        /// <summary>Checked when all copies are selected, unchecked when none, partly checked otherwise.</summary>
        public bool? IsChecked
        {
            get
            {
                int count = Duplicats.Count(d => d.IsChecked);
                if (count == 0) return false;
                if (count == Duplicats.Count) return true;
                return null;
            }
            set { }
        }

        public void SelectAllButKeeper()
        {
            var keep = Functions.PickCopyToKeep(Duplicats);
            foreach (var d in Duplicats)
                d.IsChecked = d != keep;
        }

        public void SelectNone()
        {
            foreach (var d in Duplicats)
                d.IsChecked = false;
        }

        public void UpdateTitle()
        {
            FullPath = Duplicats.Count + " copies";
            TotalFileSize = Functions.FileSize(Size * Duplicats.Count, 2);
        }

        List<FilePath_ListView> duplicats;
        public List<FilePath_ListView> Duplicats { get => duplicats; set => duplicats = value; }

        string file_type;
        public string FileType { get => file_type; set => file_type = value; }

        string total_file_size;
        public string TotalFileSize { get => total_file_size; set => total_file_size = value; }

        string file_size;
        public string FileSize { get => file_size; set => file_size = value; }
    }

    class FilePath_ListView : ListItem
    {
        public FilePath_ListView(FileInfo item)
        {
            FullPath = item.FullName;
            File = item;
            IsChecked = false;
            Image = Functions.GetFileImage24(item.Extension);
            // What the file looked like at scan time; checked again before deleting.
            ScanLength = item.Length;
            ScanWriteTimeUtc = item.LastWriteTimeUtc;
            CreationTimeUtc = item.CreationTimeUtc;
        }

        public bool IsChecked { get; set; }
        public long ScanLength { get; }
        public DateTime ScanWriteTimeUtc { get; }
        public DateTime CreationTimeUtc { get; }
    }

    enum DuplicateFileRemoverPages
    {
        DuplicateFind = 1,
        IsSimilar = 2
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
