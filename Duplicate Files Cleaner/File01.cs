using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Duplicate_Files_Cleaner
{
    class ListItem
    {
        internal ListItem() { }

        internal ListItem(FileInfo file, string item_header)
        {
            FullPath = item_header;//file name or file path;
            Image = Functions.GetFileImage24(file.Extension);
            File = file == null ? new FileInfo(file_path) : file;
            //isDirectory = false;
        }

        public ListItem(string item_header, DirectoryInfo directory)
        {
            FullPath = item_header;      //file name or file path;
            Image = Functions.GetFileImage24("directory");
            Directory = directory == null ? new DirectoryInfo(item_header) : directory;
            //isDirectory = true;
        }

        private String file_path;
        public string FullPath
        {
            get { return file_path; }
            set { file_path = value; }
        }

        private BitmapImage img_source;
        public BitmapImage Image
        {
            get { return img_source; }
            set { img_source = value; }
        }

        public FileInfo File { get; set; }

        public DirectoryInfo Directory { get; set; }

        //public bool isDirectory;

        //public int CompareTo(object obj)
        //{
        //    GeneralPurpose x = (GeneralPurpose)obj;
        //    if (x.isDirectory)
        //        if (this.isDirectory)
        //            return (x.Directory.FullName.CompareTo(this.Directory.FullName));
        //        else
        //            return 1;
        //    else
        //        if (this.isDirectory)
        //            return -1;
        //        else
        //            return (x.File.FullName.CompareTo(this.File.FullName));
        //}

        //public override bool Equals(object obj)
        //{
        //    if (obj != null)
        //    {
        //        GeneralPurpose x = (GeneralPurpose)obj;
        //        if (x.isDirectory)
        //            if (this.isDirectory)
        //                return (x.Directory.FullName.Equals(this.Directory.FullName));
        //            else
        //                return false;
        //        else
        //            if (this.isDirectory)
        //                return false;
        //            else
        //                return (x.File.FullName.Equals(this.File.FullName));
        //    }
        //    return false;
        //}
    }

    static class Functions
    {
        public static BitmapImage GetFileImage24(string extension)
        {
            if (extension.ToLower() == "directory")
                return new BitmapImage(new Uri(@"Assets\Icons\directory.png", UriKind.Relative));
            else
                return new BitmapImage(new Uri(@"Assets\Icons\file.png", UriKind.Relative));
        }

        public static BitmapImage GetFileImage256(string extension)
        {
            if (extension.ToLower() == "directory")
                return new BitmapImage(new Uri(@"Assets\Icons\directory.png", UriKind.Relative));
            else
                return new BitmapImage(new Uri(@"Assets\Icons\file.png", UriKind.Relative));
        }

        public static string GetFileType(string extension)
        {
            return "File";
            //if (FileInfoCombonents.FilesInformation.ContainsKey(extension.ToLower()))
            //    return FileInfoCombonents.FilesInformation[extension.ToLower()].Description;
            //else if (extension.Length > 2)
            //    return extension.ToUpper().Remove(0, 1) + " File";
            //else return "File";
        }

        internal static string FileSize(double SizeInByte, int Round)
        {
            if (SizeInByte < 1)
                return "0 byte";
            byte i = 0;
            while (SizeInByte / 1024 > 1)
            {
                SizeInByte = SizeInByte / 1024;
                i++;
            }
            return string.Format("{0} {1}", Math.Round(SizeInByte, Round), FileSizeUnit(i, SizeInByte));
        }

        private static string FileSizeUnit(byte Num, double size)
        {
            switch (Num)
            {
                case 0:
                    return "byte" + (size == 1 ? "" : "s");
                case 1:
                    return "KiB";
                case 2:
                    return "MiB";
                case 3:
                    return "GiB";
                case 4:
                    return "TiB";
                case 5:
                    return "PiB";
                case 6:
                    return "EiB";
                case 7:
                    return "ZiB";
                case 8:
                    return "YiB";
                default:
                    return "byte" + (size == 1 ? "" : "s");
            }
        }

        public static void ShuckWindow(Window window, bool show_messege_condition, string message)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;

            bw.DoWork += (object sender, DoWorkEventArgs args) =>
            {
                bw.ReportProgress(+10);
                Thread.Sleep(50);
                bw.ReportProgress(-20);
                Thread.Sleep(50);
                bw.ReportProgress(+20);
                Thread.Sleep(50);
                bw.ReportProgress(-20);
                Thread.Sleep(50);
                bw.ReportProgress(+20);
                Thread.Sleep(50);
                bw.ReportProgress(-10);
                Thread.Sleep(50);
            };

            bw.ProgressChanged += (object sender, ProgressChangedEventArgs args) =>
            {
                DoubleAnimation step02 = new DoubleAnimation(window.Left, window.Left + args.ProgressPercentage, new Duration(new TimeSpan(0, 0, 0, 0, 20)));
                window.BeginAnimation(Window.LeftProperty, step02);
            };

            bw.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs args) =>
            {
                if (show_messege_condition)
                    MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            bw.RunWorkerAsync();
        }

        static bool is_running = false;
        static bool stop_op = false;

        public static void TabChange(StackPanel Container, Label Sender, Rectangle Bar, int old_label, int new_label)
        {
            BackgroundWorker BW = new BackgroundWorker();
            BW.WorkerReportsProgress = true;

            double width = 0;
            for (int i = Math.Min(old_label, new_label); i <= Math.Max(old_label, new_label); i++)
                width += ((Label)Container.Children[i]).ActualWidth;

            double old_left = Bar.Margin.Left;
            double new_left = 0;
            foreach (Label i in Container.Children)
                if (i != Sender)
                    new_left += i.ActualWidth;
                else
                    break;

            BW.DoWork += (object sender, DoWorkEventArgs args) =>
            {
                for (double i = 1.36; i < 10.01; i += 0.36)
                {
                    if (stop_op) return;
                    BW.ReportProgress((int)Math.Round(100 * Math.Log10(i)));
                    Thread.Sleep(1);
                }
                Thread.Sleep(20);
                for (double i = 1.045; i < 10.01; i += 0.045)       //100 * log(10) = 100//;    //100 * log(1) = 0//;
                {
                    if (stop_op) return;
                    BW.ReportProgress((int)Math.Round(1000 + 100 * Math.Log10(i)));
                    Thread.Sleep(1);
                }
            };

            BW.ProgressChanged += (object sender, ProgressChangedEventArgs args) =>
            {
                double value = args.ProgressPercentage;
                if (value < 1000)
                {
                    if (old_label < new_label)
                    {
                        Bar.Width = ((Container.Children[old_label] as Label).ActualWidth) + (width - ((Container.Children[old_label] as Label).ActualWidth)) * value / 100.0;
                    }
                    else if (old_label > new_label)
                    {
                        Bar.Margin = new Thickness(old_left - (old_left - new_left) * 0.01 * value, 32, 0, 0);
                        Bar.Width = ((Container.Children[old_label] as Label).ActualWidth) + (width - ((Container.Children[old_label] as Label).ActualWidth)) * args.ProgressPercentage / 100;
                    }
                }
                else
                {
                    value -= 1000;
                    if (old_label < new_label)
                    {
                        Bar.Width = ((Container.Children[new_label] as Label).ActualWidth) + (width - ((Container.Children[new_label] as Label).ActualWidth)) * (100 - value) * 0.01;
                        Bar.Margin = new Thickness(old_left + (new_left - old_left) * 0.01 * value, 32, 0, 0);
                    }
                    else if (old_label > new_label)
                    {
                        Bar.Width = ((Container.Children[new_label] as Label).ActualWidth) + (width - ((Container.Children[new_label] as Label).ActualWidth)) * (100 - value) * 0.01;
                    }
                }

            };

            BW.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs args) =>
            {
                is_running = false;
                stop_op = false;
            };

            if (is_running)
            {
                stop_op = true;
                Bar.Margin = new Thickness(new_left, 32, 0, 0);
                Bar.Width = width;
            }
            else
                BW.RunWorkerAsync();

            foreach (Label i in Container.Children)
            {
                i.Background = Brushes.Black;
                i.Foreground = Brushes.White;
            }
            Sender.Background = Brushes.White;
            Sender.Foreground = Brushes.Black;
        }

    }

}
