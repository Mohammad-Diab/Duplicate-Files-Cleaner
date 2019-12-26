using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Duplicate_Files_Cleaner
{
    class Duplicate
    {
        StringBuilder log;
        public string Log { get => log.ToString(); }

        public double Persent;
        public string CurrentAction;

        bool cancel;
        public bool StopOperation { get => cancel; }

        static bool ison;
        public bool IsON { get => ison; }

        static bool pause;
        public bool Pause { get => pause; }

        string  action_before_pause;

        int buffer = 32768;

        public Duplicate(int BufferSize)
        {
            buffer = BufferSize;
            log = new StringBuilder(string.Empty);
            Persent = 0;
            CurrentAction = "Initializing...";
            cancel = false;
            ison = false;
            pause = false;
            action_before_pause = "Initializing...";
        }


        public void PauseOperation()
        {
            action_before_pause = CurrentAction;
            CurrentAction = "Paused";
            pause = true;
        }

        public void ResumeOperation()
        {
            pause = false;
            CurrentAction = action_before_pause;
        }

        public void CancelOperation()
        {
            CurrentAction = "Canceled.";
            Persent = 0;
            cancel = true;
            Thread.Sleep(500);
            log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Duplicate Files Cleaner", "Operation canceled by user.", Environment.NewLine);
        }

        public List<Duplicate_Group> FindDuplicate(List<string> pathes, int stage, bool hidden_files, bool system_files, bool html_files, bool special_folders, string[] special_folders_list)
        {
            ison = true;
            log.Clear();
            Persent = 0;
            CurrentAction = "Preparing...";
            cancel = false;
            pause = false;

            log.AppendFormat("{0}{1}", "Duplicate files' cleaner", Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "Version:", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(), Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "By:", "Dr.Mohammad@outlook.com", Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "Operation started at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
            log.AppendLine();
            log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "getting folders contents started.", Environment.NewLine);

            var groups = new List<Duplicate_Group>();
            List<long> indexes = new List<long>();

            CurrentAction = "Reading folders contents...";

            int[] options = { hidden_files ? (int)FileAttributes.Hidden : 0, system_files ? (int)FileAttributes.System : 0, html_files ? 1 : 0, 0 };

            Dictionary<string, bool> special = new Dictionary<string, bool>();
            if (special_folders)
            {
                foreach (var i in special_folders_list)
                {
                    if (i != string.Empty && Directory.Exists(i))
                        special.Add(i.EndsWith("\\") ? i.ToLower() : i.ToLower() + "\\", true);
                    if (StopOperation)
                    {
                        ison = false;
                        return groups;
                    }
                }
            }
            

            foreach (var i in pathes)
            {
                GetFiles(i, groups, indexes, 3.0 / pathes.Count, special, options);
                if (StopOperation)
                {
                    ison = false;
                    return groups;
                }
            }
            log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "getting folders contents completed.", Environment.NewLine);

            if (groups.Count == 0)
            {
                Persent = 100;
                CurrentAction = "Finished.";
                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "no files detected!", Environment.NewLine);
                log.AppendLine();
                log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
                log.AppendFormat("{0}\t {1}{2}", "Preparing files", "no duplicates found!", Environment.NewLine);
                ison = false;
                return groups;
            }
            else
            {
                //stage 1: compare files length
                CurrentAction = "Grouping files...";
                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Grouping files started.", Environment.NewLine);
                for (int i = groups.Count - 1; i > -1; i--)
                {
                    if (groups[i].Items.Count < 2)
                        groups.RemoveAt(i);
                }
                Persent = 10;

                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Grouping files completed.", Environment.NewLine);

                if (groups.Count == 0)
                {
                    Persent = 100;
                    CurrentAction = "Finished.";
                    log.AppendLine();
                    log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
                    log.AppendFormat("{0}\t {1}{2}", "Stage 1", "no duplicates found!", Environment.NewLine);
                    ison = false;
                    return groups;
                }

                //stage 2: get files parts

                if (stage > 1)
                {
                    CurrentAction = "Reading and comparing files phase 1...";
                    log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading and comparing files phase '1' stated.", Environment.NewLine);

                    int groups_count = groups.Count;
                    for (int i = groups.Count - 1; i > -1; i--)
                    {
                        if (StopOperation)
                        {
                            ison = false;
                            return groups;
                        }
                        bool _continue = false;
                        int items_count = groups[i].Items.Count;
                        for (int j = groups[i].Items.Count - 1; j > -1; j--)
                        {
                            if (StopOperation)
                            {
                                ison = false;
                                return groups;
                            }
                            var part_result = GetParts(groups[i].Items[j]);
                            if (!(part_result.HasValue && part_result.Value))
                                groups[i].Items.RemoveAt(j);
                            if (groups[i].Items.Count < 2)
                            {
                                groups.RemoveAt(i);
                                _continue = true;
                                continue;
                            }
                            Persent += 45.0 / (groups_count * items_count);
                        }

                        groups[i].Items.Sort((x, y) => { return x.FilePeek.CompareTo(y.FilePeek); });
                        Persent += 5.0 / groups_count;
                        if (_continue) continue;

                        int duplicate_groups = 0;
                        int duplicate_items = 0;

                        items_count = groups[i].Items.Count;
                        for (int j = groups[i].Items.Count - 1; j > -1; j--)
                        {
                            if (StopOperation)
                            {
                                ison = false;
                                return groups;
                            }
                            for (int k = j - 1; k > -1; k--)
                            {
                                if (StopOperation)
                                {
                                    ison = false;
                                    return groups;
                                }
                                if (groups[i].Items[j].FilePeek.Equals(groups[i].Items[k].FilePeek))
                                    duplicate_items++;
                                else
                                    break;
                            }
                            if (duplicate_items > 0)
                            {
                                if (duplicate_groups > 0)
                                {
                                    var new_group = new Duplicate_Group(groups[i].Size);
                                    new_group.Items.AddRange(groups[i].Items.GetRange(j - duplicate_items, duplicate_items + 1));
                                    groups.Add(new_group);
                                    groups[i].Items.RemoveRange(j - duplicate_items, duplicate_items + 1);
                                }
                                j -= duplicate_items;
                                duplicate_groups++;
                                duplicate_items = 0;
                            }
                            else
                                groups[i].Items.RemoveAt(j);
                            if (groups[i].Items.Count < 2)
                            {
                                groups.RemoveAt(i);
                                break;
                            }
                            Persent += 9.0 / (groups_count * items_count);
                        }
                    }
                    log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading and comparing files phase '1' completed.", Environment.NewLine);

                    Persent = 70;


                    if (groups.Count == 0)
                    {
                        Persent = 100;
                        CurrentAction = "Finished.";
                        log.AppendLine();
                        log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
                        log.AppendFormat("{0}\t {1}{2}", "Stage 2", "no duplicates found!", Environment.NewLine); ison = false;
                        return groups;
                    }

                    //stage 3:  compare hashes
                    if (stage == 3)
                    {
                        CurrentAction = "Reading and comparing files phase 2...";
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 3", "Reading and comparing files phase '2' started.", Environment.NewLine);
                        groups_count = groups.Count;
                        for (int i = groups.Count - 1; i > -1; i--)
                        {
                            int items_count = groups[i].Items.Count;
                            bool _continue = false;
                            if (StopOperation)
                            {
                                ison = false;
                                return groups;
                            }
                            for (int j = groups[i].Items.Count - 1; j > -1; j--)
                            {
                                if (StopOperation)
                                {
                                    ison = false;
                                    return groups;
                                }
                                if (groups[i].Items.Count < 2)
                                {
                                    groups.RemoveAt(i);
                                    _continue = true;
                                    continue;
                                }
                                var hash_result = GetHash(groups[i].Items[j]);
                                if (!(hash_result.HasValue && hash_result.Value))
                                    groups[i].Items.RemoveAt(j);
                                if (groups[i].Items.Count < 2)
                                {
                                    groups.RemoveAt(i);
                                    _continue = true;
                                    continue;
                                }
                                Persent += 17.0 / (groups_count * items_count);
                            }
                            groups[i].Items.Sort((x, y) => { return x.Hashes.CompareTo(y.Hashes); });
                            Persent += 3.0 / groups_count;
                            if (_continue) continue;

                            int duplicate_groups = 0;
                            int duplicate_items = 0;

                            items_count = groups[i].Items.Count;
                            for (int j = groups[i].Items.Count - 1; j > -1; j--)
                            {
                                if (StopOperation)
                                {
                                    ison = false;
                                    return groups;
                                }
                                for (int k = j - 1; k > -1; k--)
                                {
                                    if (StopOperation)
                                    {
                                        ison = false;
                                        return groups;
                                    }
                                    if (groups[i].Items[j].Hashes.Equals(groups[i].Items[k].Hashes))
                                        duplicate_items++;
                                    else
                                        break;
                                }
                                if (duplicate_items > 0)
                                {
                                    if (duplicate_groups > 0)
                                    {
                                        var new_group = new Duplicate_Group(groups[i].Size);
                                        new_group.Items.AddRange(groups[i].Items.GetRange(j - duplicate_items, duplicate_items + 1));
                                        groups.Add(new_group);
                                        groups[i].Items.RemoveRange(j - duplicate_items, duplicate_items + 1);
                                    }
                                    j -= duplicate_items;
                                    duplicate_groups++;
                                    duplicate_items = 0;
                                }
                                else
                                    groups[i].Items.RemoveAt(j);
                                if (groups[i].Items.Count < 2)
                                {
                                    groups.RemoveAt(i);
                                    break;
                                }
                                Persent += 4.0 / (groups_count * items_count);
                            }
                        }
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 3", "Reading and comparing files phase '2' completed.", Environment.NewLine);

                        Persent = 95;
                        if (groups.Count == 0)
                        {
                            Persent = 100;
                            CurrentAction = "Finished.";
                            log.AppendLine();
                            log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
                            log.AppendFormat("{0}\t {1}{2}", "Stage 3", "no duplicates found!", Environment.NewLine); ison = false; return groups;
                        }
                    }
                    else if (stage == 4)
                    {
                        //compare files

                    }
                    CurrentAction = "Finished.";
                    log.AppendLine();
                    log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
                    if (groups.Count > 0)
                        log.AppendFormat("{0} {1} {2}{3}", "Found", groups.Count, "Duplicates", Environment.NewLine);
                }
                ison = false;
                return groups;
            }
        }

        private void GetFiles(string path, List<Duplicate_Group> groups, List<long> indexes, double progress, Dictionary<string, bool> special_folder, int[] options)
        {
            //options
            //[0]= hidden
            //[1]= system
            //[2]= html //don't check for it here just in sub directory
            //[3]= special folders  //empty dictioary if false

            if (Directory.Exists(path))
            {
                log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "getting", path, "contents.", Environment.NewLine);
                DirectoryInfo dr = new DirectoryInfo(path);
                int[] option_for_drive = { 0, 0, 0, 0 };
                if (CheckFolder(dr, null, dr.Parent == null ? new Dictionary<string, bool>() : special_folder, dr.Parent == null ? option_for_drive : options))
                    GetFiles(dr, groups, indexes, progress, special_folder, options);
            }
            else
                log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", path, "is not exists.", Environment.NewLine);
        }

        private void GetFiles(DirectoryInfo dirctory, List<Duplicate_Group> groups, List<long> indexes, double progress, Dictionary<string, bool> special_folder, params int[] options)
        {
            if (dirctory.Exists)
            {
                try
                {
                    var dirs = dirctory.GetDirectories();
                    var files = dirctory.GetFiles();

                    foreach (var i in dirs)
                    {
                        if (StopOperation)
                        {
                            ison = false;
                            return;
                        }
                        if (CheckFolder(i, files, special_folder, options))
                            GetFiles(i, groups, indexes, progress / dirs.Length, special_folder, options);
                    }


                    foreach (var i in files)
                    {
                        if (StopOperation)
                        {
                            ison = false;
                            return;
                        }
                        if (CheckFile(i, options))
                        {
                            int index = -1;
                            if ((index = indexes.IndexOf(i.Length)) > -1)
                            {
                                if (groups[index].Size == i.Length)
                                    groups[index].Items.Add(new Duplicate_item(i));
                            }
                            else
                            {
                                var item = new Duplicate_Group(i.Length);
                                item.Items.Add(new Duplicate_item(i));
                                groups.Add(item);
                                indexes.Add(i.Length);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "can't read", dirctory.FullName, "contents.", Environment.NewLine);
                }
            }
            else
                log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", "folder", dirctory.FullName, "is not exists.", Environment.NewLine);

            Persent += progress;
        }

        private bool CheckFolder(DirectoryInfo i,FileInfo[] files, Dictionary<string, bool> special_folder, int[] options)
        {
            if ((i.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                if ((i.Attributes & (FileAttributes)options[0]) == 0)
                {
                    if ((i.Attributes & (FileAttributes)options[1]) == 0)
                    {
                        if (!special_folder.ContainsKey((i.FullName.EndsWith("\\")) ? i.FullName.ToLower() : i.FullName.ToLower() + '\\'))
                        {
                            if (files == null || CheckHTMLFolder(options[2], i, files))
                                return true;
                            else
                                log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is an HTML document folder.", Environment.NewLine);
                        }
                        else
                            log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is one of special folders.", Environment.NewLine);
                    }
                    else
                        log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a system folder.", Environment.NewLine);
                }
                else
                    log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a hidden folder.", Environment.NewLine);
            }
            else
                log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a junction.", Environment.NewLine);
            return false;
        }

        private bool CheckFile(FileInfo i, int[] options)
        {
            if ((i.Attributes & FileAttributes.ReparsePoint) == 0)
                if ((i.Attributes & (FileAttributes)options[0]) == 0)
                    if ((i.Attributes & (FileAttributes)options[1]) == 0)
                        return true;
                    else
                        log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a system file.", Environment.NewLine);
                else
                    log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a hidden file.", Environment.NewLine);
            else
                log.AppendFormat("{0}\t{1}\t{2}:\t'{3}' {4}{5}", "-w-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Files Explorer", i.FullName, "is a symbolic link.", Environment.NewLine);
            return false;
        }

        private bool CheckHTMLFolder(int option, DirectoryInfo di, FileInfo[] files)
        {
            if (option > 0)
                if (di.Name.EndsWith("_files", StringComparison.OrdinalIgnoreCase))
                {
                    string html_file_name1 = di.Name.Substring(0, di.Name.Length - 6) + ".htm";
                    string html_file_name2 = di.Name.Substring(0, di.Name.Length - 6) + ".html";
                    foreach (var i in files)
                        if (i.Name.Equals(html_file_name1, StringComparison.OrdinalIgnoreCase) || i.Name.Equals(html_file_name2, StringComparison.OrdinalIgnoreCase))
                            return false;
                        
                }
            return true;
        }

        private bool? GetParts(Duplicate_item item)
        {
            if (string.IsNullOrEmpty(item.FilePeek))
                return true;
            BinaryReader bw = null;
            try
            {
                if (!item.File.Exists)
                {
                    log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "file", item.File.FullName, "is not exists.", Environment.NewLine);
                    return false;
                }
                bw = new BinaryReader(item.File.OpenRead());
                if (item.File.Length < 51)
                    item.FilePeek = BitConverter.ToString(bw.ReadBytes((int)item.File.Length));
                else
                {
                    byte[] block;
                    double max = Math.Ceiling(2 * Math.Log(item.File.Length));
                    for (int i = 0; i < max; i++)
                    {
                        if (StopOperation)
                        {
                            bw.Close();
                            ison = false;
                            return null;
                        }
                        if (bw.BaseStream.CanSeek)
                            bw.BaseStream.Seek((long)Math.Round(item.File.Length * i / max), SeekOrigin.Begin);
                        block = bw.ReadBytes(4);
                        item.FilePeek += BitConverter.ToString(block);
                        if (i == max - 1)
                            item.FilePeek += "-";
                    }
                }
                bw.Close();
                return true;
            }
            catch (Exception)
            {
                if (bw != null)
                    bw.Close();
                log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "file", item.File.FullName, "is not readable.", Environment.NewLine);
                return false;
            }
        }

        private bool? GetHash(Duplicate_item item)
        {
            BinaryReader bw = null;
            if (!item.File.Exists)
            {
                log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 3", "file", item.File.FullName, "is not exists.", Environment.NewLine);
                return false;
            }
            if (item.File.Length < 51)
            {
                item.Hashes = "";
                return true;
            }
            try
            {
                SHA512 SHA = SHA512.Create();
                MD5 MD5 = MD5.Create();
                bw = new BinaryReader(item.File.OpenRead());
                long i;
                byte[] block;
                for (i = 0; i < (item.File.Length / buffer) - 1; i++)
                {
                    if (StopOperation)
                    {
                        ison = false;
                        return null;
                    }
                    block = bw.ReadBytes(buffer);
                    SHA.TransformBlock(block, 0, block.Length, block, 0);
                    MD5.TransformBlock(block, 0, block.Length, block, 0);
                }
                if (StopOperation)
                {
                    ison = false;
                    return null;
                }
                block = bw.ReadBytes((int)(item.File.Length - (i * buffer)));
                SHA.TransformFinalBlock(block, 0, block.Length);
                MD5.TransformFinalBlock(block, 0, block.Length);
                item.Hashes = BitConverter.ToString(SHA.Hash.Concat(MD5.Hash).ToArray());
                bw.Close();
                return true;
            }
            catch (Exception)
            {
                if (bw != null)
                    bw.Close();
                log.AppendFormat("{0}\t{1}\t{2}:\t{3} '{4}' {5}{6}", "-e-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 3", "file", item.File.FullName, "is not readable.", Environment.NewLine);
                return false;
            }
        }

        internal IsSameResult IsSame(FileInfo first_file, FileInfo second_file, int stage)
        {
            ison = true;
            log.Clear();
            Persent = 0;
            CurrentAction = "Preparing...";
            cancel = false;
            pause = false;

            log.AppendFormat("{0}{1}", "Duplicate files' cleaner,\tIs Same Tool", Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "Version:", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(), Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "By:", "Dr.Mohammad@outlook.com", Environment.NewLine);
            log.AppendFormat("{0} {1}{2}", "Operation started at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
            log.AppendLine();

            Duplicate_item first = new Duplicate_item(first_file);
            Duplicate_item second = new Duplicate_item(second_file);

            CurrentAction = "Comparing Files...";
            Persent = 0;

            if (first_file.Exists && second_file.Exists)
            {
                if (first_file.Length == second_file.Length)
                {
                    Persent = 5;
                    if (stage > 1)
                    {
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Reading first file phase '1' stated.", Environment.NewLine);
                        var part1 = GetParts(first);
                        if (!part1.HasValue)
                            return FinishIsSame(IsSameResult.Canceled);
                        else if (!part1.Value)
                            return FinishIsSame(IsSameResult.CanNotReadFile);
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Reading first file phase '1' completed.", Environment.NewLine);
                        Persent = 15;
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Reading second file phase '1' stated.", Environment.NewLine);
                        var part2 = GetParts(second);
                        if (!part2.HasValue)
                            return FinishIsSame(IsSameResult.Canceled);
                        else if (!part2.Value)
                            return FinishIsSame(IsSameResult.CanNotReadFile);
                        log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 1", "Reading first file phase '1' completed.", Environment.NewLine);
                        Persent = 25;

                        if (first.FilePeek.Equals(second.FilePeek))
                        {
                            Persent = 30;
                            if (stage == 3)
                            {
                                Persent = 35;
                                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading first file phase '2' stated.", Environment.NewLine);
                                var hash1 = GetHash(first);
                                if (!hash1.HasValue)
                                    return FinishIsSame(IsSameResult.Canceled);
                                else if (!hash1.Value)
                                    return FinishIsSame(IsSameResult.CanNotReadFile);
                                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading first file phase '2' completed.", Environment.NewLine);
                                Persent = 65;
                                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading second file phase '1' started.", Environment.NewLine);
                                var hash2 = GetHash(second);
                                if (!hash2.HasValue)
                                    return FinishIsSame(IsSameResult.Canceled);
                                else if (!hash2.Value)
                                    return FinishIsSame(IsSameResult.CanNotReadFile);
                                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading second file phase '2' completed.", Environment.NewLine);
                                Persent = 95;
                                if (first.Hashes.Equals(second.Hashes))
                                    return FinishIsSame(IsSameResult.Same);
                                else
                                    return FinishIsSame(IsSameResult.NotSame);
                            }
                            else if (stage == 4)
                            {
                                log.AppendFormat("{0}\t{1}\t{2}:\t{3}{4}", "-i-", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), "Stage 2", "Reading and comparing files phase '2'.", Environment.NewLine);
                                return CompareFilesByteByByte(first, second);
                            }
                            else return FinishIsSame(IsSameResult.Same);
                        }
                        else return FinishIsSame(IsSameResult.NotSame);
                    }
                    else return FinishIsSame(IsSameResult.Same);
                }
                else return FinishIsSame(IsSameResult.NotSame);
            }
            else return FinishIsSame(IsSameResult.FileIsNotExist);
        }

        private IsSameResult CompareFilesByteByByte(Duplicate_item first, Duplicate_item second)
        {
            BinaryReader bw1 = null;
            BinaryReader bw2 = null;
            try
            {
                bw1 = new BinaryReader(first.File.OpenRead());
                bw2 = new BinaryReader(second.File.OpenRead());
                long i;
                for (i = 0; i < (first.File.Length / buffer) - 1; i++)
                {
                    if (StopOperation)
                    {
                        bw1.Close();
                        bw2.Close();
                        return FinishIsSame(IsSameResult.Canceled);
                    }
                    first.binary_file = bw1.ReadBytes(buffer);
                    second.binary_file = bw2.ReadBytes(buffer);
                    if (!first.binary_file.SequenceEqual(second.binary_file))
                        return FinishIsSame(IsSameResult.NotSame);
                }
                if (StopOperation)
                {
                    bw1.Close();
                    bw2.Close();
                    return FinishIsSame(IsSameResult.Canceled);
                }
                first.binary_file = bw1.ReadBytes((int)(first.File.Length - (i * buffer)));
                second.binary_file = bw2.ReadBytes((int)(second.File.Length - (i * buffer)));
                bw1.Close();
                bw2.Close();
                if (!first.binary_file.SequenceEqual(second.binary_file))
                    return FinishIsSame(IsSameResult.NotSame);
                return FinishIsSame(IsSameResult.Same);
            }
            catch (Exception)
            {
                if (bw1 != null)
                    bw1.Close();
                if (bw2 != null)
                    bw2.Close();
                return FinishIsSame(IsSameResult.CanNotReadFile);
            }
        }

        private IsSameResult FinishIsSame(IsSameResult result)
        {
            CurrentAction = "Finished.";
            log.AppendLine();
            log.AppendFormat("{0} {1}{2}", "Operation finished at:", DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss"), Environment.NewLine);
            switch (result)
            {
                case IsSameResult.Same:
                    log.AppendFormat("{0}{1}", "These files are same.", Environment.NewLine);
                    break;
                case IsSameResult.NotSame:
                    log.AppendFormat("{0}{1}", "These files aren't same.", Environment.NewLine);
                    break;
                case IsSameResult.Canceled:
                    break;
                default:
                    log.AppendFormat("{0}{1}", "Some thing went wrong.", Environment.NewLine);
                    break;
            }
            Persent = 100;
            ison = false;
            return result;
        }

    }

    class Duplicate_Group
    {
        long file_size;
        public long Size { get => file_size; set => file_size = value; }

        public List<Duplicate_item> Items;

        public Duplicate_Group(long size)
        {
            Size = size;
            Items = new List<Duplicate_item>();
        }
    }

    class Duplicate_item
    {
        FileInfo file;
        public FileInfo File { get => file; set => file = value; }

        string parts;
        public string FilePeek { get => parts; set => parts = value; }

        string hashes;
        public string Hashes { get => hashes; set => hashes = value; }

        public byte[] binary_file;

        public Duplicate_item(FileInfo file)
        {
            File = file;
            FilePeek = "";
            Hashes = "";
        }

    }

    enum IsSameResult
    {
        Same,
        NotSame,
        FileIsNotExist,
        CanNotReadFile,
        Canceled,
        Unknown
    }

}
