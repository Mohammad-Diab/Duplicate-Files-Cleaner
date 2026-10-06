using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Duplicate_Files_Cleaner
{
    /// <summary>
    /// Scan modes, from the lightest to the strictest. The numbers are the values saved in Settings.scan_quality.
    /// </summary>
    enum ScanMode
    {
        SizeOnly = 1,   // same size = duplicate
        Sample = 2,     // same size + same bytes at sampled positions
        Smart = 3,      // + same SHA-512 hash of the whole file
        Full = 4        // + byte-by-byte comparison of every copy with the first one
    }

    class ScanOptions
    {
        public ScanMode Mode = ScanMode.Smart;
        public bool SkipHidden = true;
        public bool SkipSystem = true;
        public bool SkipWebPageFolders = false;
        public List<string> ExcludedFolders = new List<string>();
    }

    class Duplicate
    {
        ScanLog log = new ScanLog();
        public string Log { get => log.ToString(); }

        // Read by the UI thread while a scan runs.
        public volatile bool Indeterminate;
        double persent;
        public double Persent { get => persent; }
        public volatile string CurrentAction = "";

        volatile bool cancel;
        public bool StopOperation { get => cancel; }

        volatile bool ison;
        public bool IsON { get => ison; }

        public int BufferSize { get; set; }

        long bytes_done, bytes_total;

        public Duplicate(int buffer_size)
        {
            BufferSize = buffer_size;
        }

        public void CancelOperation()
        {
            cancel = true;
            CurrentAction = "Canceling...";
        }

        void Start(string action)
        {
            ison = true;
            cancel = false;
            persent = 0;
            Indeterminate = true;
            CurrentAction = action;
            if (BufferSize < 1024)
                BufferSize = 32768;
        }

        void SetProgress(double value)
        {
            Indeterminate = false;
            persent = Math.Max(0, Math.Min(100, value));
        }

        #region Find duplicates

        public List<Duplicate_Group> FindDuplicate(List<string> paths, ScanOptions options)
        {
            Start("Reading folders...");
            log = new ScanLog();
            log.Begin("Scan", paths, options);
            var result = new List<Duplicate_Group>();
            try
            {
                result = Find(paths, options);
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error", "", ex.Message);
                result.Clear();
            }
            log.End(cancel, result);
            persent = 100;
            Indeterminate = false;
            CurrentAction = cancel ? "Canceled." : "Finished.";
            ison = false;
            return cancel ? new List<Duplicate_Group>() : result;
        }

        List<Duplicate_Group> Find(List<string> paths, ScanOptions options)
        {
            // Step 1: read the folders and group the files by size.
            log.StepStart("Reading folders");
            var excluded = new HashSet<string>(options.ExcludedFolders.Select(NormalizeFolder), StringComparer.OrdinalIgnoreCase);
            var by_size = new Dictionary<long, Duplicate_Group>();
            foreach (var path in paths)
            {
                ReadFolder(path, options, excluded, by_size);
                if (cancel) return new List<Duplicate_Group>();
            }
            log.StepEnd(string.Format("{0:N0} files, {1}", log.FilesScanned, Functions.FileSize(log.BytesScanned, 2)));

            var groups = by_size.Values.Where(g => g.Items.Count > 1).ToList();
            log.StepStart("Grouping by size");
            log.StepEnd(string.Format("{0:N0} files in {1:N0} groups have the same size as another file", groups.Sum(g => g.Items.Count), groups.Count));

            // Step 2: the same file reached through two paths (hard links, or a folder chosen twice through a junction)
            // is one file, not a duplicate. Deleting one of them would free nothing.
            CurrentAction = "Checking hard links...";
            log.StepStart("Checking hard links");
            int links = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                if (cancel) return new List<Duplicate_Group>();
                var seen = new Dictionary<string, Duplicate_item>();
                for (int i = groups[g].Items.Count - 1; i > -1; i--)
                {
                    var item = groups[g].Items[i];
                    string id = NativeFile.GetFileId(item.File.FullName);
                    if (id == null) continue;
                    Duplicate_item other;
                    if (seen.TryGetValue(id, out other))
                    {
                        log.Skip("Same file (hard link)", item.File.FullName + "  =  " + other.File.FullName);
                        groups[g].Items.RemoveAt(i);
                        links++;
                    }
                    else
                        seen.Add(id, item);
                }
            }
            groups = groups.Where(g => g.Items.Count > 1).ToList();
            log.StepEnd(links == 0 ? "none found" : string.Format("{0:N0} hard link{1} ignored", links, links == 1 ? "" : "s"));

            if (options.Mode == ScanMode.SizeOnly || groups.Count == 0)
                return Sort(groups);

            // Step 3: compare a sample of bytes from each file.
            CurrentAction = "Comparing file samples...";
            log.StepStart("Comparing samples");
            int total_items = groups.Sum(g => g.Items.Count), done_items = 0;
            double sample_share = options.Mode == ScanMode.Sample ? 100 : 10;
            foreach (var group in groups)
                foreach (var item in group.Items.ToList())
                {
                    if (cancel) return new List<Duplicate_Group>();
                    if (!ReadSample(item))
                        group.Items.Remove(item);
                    SetProgress(sample_share * ++done_items / total_items);
                }
            groups = Split(groups, i => i.FilePeek);
            log.StepEnd(string.Format("{0:N0} groups left", groups.Count));

            if (options.Mode == ScanMode.Sample || groups.Count == 0)
                return Sort(groups);

            // Step 4: hash the whole content. Files of 4 KiB or less were read completely in step 3.
            double hash_end = options.Mode == ScanMode.Full ? 60 : 100;
            CurrentAction = "Hashing files...";
            log.StepStart("Hashing (SHA-512)");
            bytes_done = 0;
            bytes_total = groups.Sum(g => g.Items.Where(i => i.File.Length > SampleWholeFileLimit).Sum(i => i.File.Length));
            foreach (var group in groups)
                foreach (var item in group.Items.ToList())
                {
                    if (cancel) return new List<Duplicate_Group>();
                    CurrentAction = string.Format("Hashing files... {0} of {1}", Functions.FileSize(bytes_done, 1), Functions.FileSize(bytes_total, 1));
                    if (!Hash(item, 10, hash_end))
                        group.Items.Remove(item);
                }
            if (cancel) return new List<Duplicate_Group>();
            groups = Split(groups, i => i.Hashes);
            log.StepEnd(string.Format("{0:N0} groups left", groups.Count));

            if (options.Mode != ScanMode.Full || groups.Count == 0)
                return Sort(groups);

            // Step 5: compare every copy byte by byte with the first one.
            CurrentAction = "Comparing files byte by byte...";
            log.StepStart("Byte-by-byte check");
            bytes_done = 0;
            bytes_total = groups.Sum(g => g.Items.Skip(1).Sum(i => i.File.Length));
            foreach (var group in groups)
            {
                var first = group.Items[0];
                foreach (var item in group.Items.Skip(1).ToList())
                {
                    if (cancel) return new List<Duplicate_Group>();
                    bool? same = SameBytes(first.File, item.File, 60, 100);
                    if (same == null)
                    {
                        log.Error("Can't read file", item.File.FullName, "");
                        group.Items.Remove(item);
                    }
                    else if (!same.Value)
                    {
                        log.Skip("Different content (same hash)", item.File.FullName);
                        group.Items.Remove(item);
                    }
                }
            }
            groups = groups.Where(g => g.Items.Count > 1).ToList();
            log.StepEnd(string.Format("{0:N0} groups confirmed", groups.Count));
            return Sort(groups);
        }

        static List<Duplicate_Group> Sort(List<Duplicate_Group> groups)
        {
            foreach (var g in groups)
                g.Items.Sort((x, y) => string.Compare(x.File.FullName, y.File.FullName, StringComparison.OrdinalIgnoreCase));
            return groups;
        }

        static List<Duplicate_Group> Split(List<Duplicate_Group> groups, Func<Duplicate_item, string> key)
        {
            var result = new List<Duplicate_Group>();
            foreach (var group in groups)
                foreach (var part in group.Items.GroupBy(key))
                    if (part.Count() > 1)
                    {
                        var g = new Duplicate_Group(group.Size);
                        g.Items.AddRange(part);
                        result.Add(g);
                    }
            return result;
        }

        static string NormalizeFolder(string path)
        {
            path = path.Trim();
            return path.EndsWith("\\") ? path : path + "\\";
        }

        void ReadFolder(string root, ScanOptions options, HashSet<string> excluded, Dictionary<long, Duplicate_Group> by_size)
        {
            if (!Directory.Exists(root))
            {
                log.Error("Folder not found", root, "");
                return;
            }
            // A folder the user chose is always read, even when it is a junction or a hidden or system folder.
            // Inside it, junctions and symbolic links are never followed, so a link can't cause a loop.
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(root));
            while (stack.Count > 0)
            {
                if (cancel) return;
                var dir = stack.Pop();
                CurrentAction = string.Format("Reading folders... {0:N0} files found", log.FilesScanned);
                FileInfo[] files;
                DirectoryInfo[] dirs;
                try
                {
                    files = dir.GetFiles();
                    dirs = dir.GetDirectories();
                }
                catch (Exception ex)
                {
                    log.Error("Can't read folder", dir.FullName, ex.Message);
                    continue;
                }
                log.FoldersScanned++;

                foreach (var d in dirs)
                {
                    string reason = FolderSkipReason(d, files, options, excluded);
                    if (reason == null)
                        stack.Push(d);
                    else
                        log.Skip(reason, d.FullName);
                }

                foreach (var f in files)
                {
                    string reason = FileSkipReason(f, options);
                    if (reason != null)
                    {
                        log.Skip(reason, f.FullName);
                        continue;
                    }
                    long length;
                    try { length = f.Length; }
                    catch (Exception ex) { log.Error("Can't read file", f.FullName, ex.Message); continue; }
                    log.FilesScanned++;
                    log.BytesScanned += length;
                    if (length == 0)
                    {
                        log.Skip("Empty file", f.FullName);
                        continue;
                    }
                    Duplicate_Group group;
                    if (!by_size.TryGetValue(length, out group))
                        by_size.Add(length, group = new Duplicate_Group(length));
                    group.Items.Add(new Duplicate_item(f));
                }
            }
        }

        static string FolderSkipReason(DirectoryInfo d, FileInfo[] files_beside, ScanOptions options, HashSet<string> excluded)
        {
            var a = d.Attributes;
            if ((a & FileAttributes.ReparsePoint) != 0) return "Junction or folder link (not followed)";
            if (options.SkipHidden && (a & FileAttributes.Hidden) != 0) return "Hidden folder";
            if (options.SkipSystem && (a & FileAttributes.System) != 0) return "System folder";
            if (excluded.Contains(NormalizeFolder(d.FullName))) return "Excluded folder";
            if (options.SkipWebPageFolders && IsWebPageFolder(d, files_beside)) return "Saved web page folder";
            return null;
        }

        static string FileSkipReason(FileInfo f, ScanOptions options)
        {
            var a = f.Attributes;
            // Symbolic links, and cloud files that are not on the disk (OneDrive "online-only"): reading them would
            // read another file or download it.
            if ((a & FileAttributes.ReparsePoint) != 0) return "Symbolic link or cloud file";
            if ((a & FileAttributes.Offline) != 0) return "Offline file";
            if (options.SkipHidden && (a & FileAttributes.Hidden) != 0) return "Hidden file";
            if (options.SkipSystem && (a & FileAttributes.System) != 0) return "System file";
            return null;
        }

        // "page_files" next to "page.htm" or "page.html": the folder a browser makes when it saves a web page.
        static bool IsWebPageFolder(DirectoryInfo d, FileInfo[] files_beside)
        {
            if (!d.Name.EndsWith("_files", StringComparison.OrdinalIgnoreCase))
                return false;
            string page = d.Name.Substring(0, d.Name.Length - 6);
            return files_beside.Any(f => f.Name.Equals(page + ".htm", StringComparison.OrdinalIgnoreCase)
                                      || f.Name.Equals(page + ".html", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Reading files

        const int SampleWholeFileLimit = 4096;

        /// <summary>Reads the whole file when it is small, or 4 bytes at about 2·ln(size) spread positions plus the last bytes.</summary>
        bool ReadSample(Duplicate_item item)
        {
            try
            {
                using (var stream = OpenRead(item.File))
                {
                    long length = stream.Length;
                    if (length <= SampleWholeFileLimit)
                    {
                        item.FilePeek = "W" + Convert.ToBase64String(ReadExactly(stream, (int)length));
                        item.Hashes = item.FilePeek;   // the whole content is known, no hash needed
                        return true;
                    }
                    var sb = new StringBuilder();
                    int count = (int)Math.Ceiling(2 * Math.Log(length));
                    for (int i = 0; i < count; i++)
                    {
                        stream.Seek((long)Math.Round((double)length * i / count), SeekOrigin.Begin);
                        sb.Append(Convert.ToBase64String(ReadExactly(stream, 4)));
                    }
                    stream.Seek(-16, SeekOrigin.End);
                    sb.Append(Convert.ToBase64String(ReadExactly(stream, 16)));
                    item.FilePeek = sb.ToString();
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error("Can't read file", item.File.FullName, ex.Message);
                return false;
            }
        }

        bool Hash(Duplicate_item item, double from, double to)
        {
            if (item.File.Length <= SampleWholeFileLimit && !string.IsNullOrEmpty(item.Hashes))
                return true;
            try
            {
                using (var sha = SHA512.Create())
                using (var stream = OpenRead(item.File))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancel) return false;
                        sha.TransformBlock(buffer, 0, read, null, 0);
                        bytes_done += read;
                        if (bytes_total > 0)
                            SetProgress(from + (to - from) * bytes_done / bytes_total);
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    item.Hashes = Convert.ToBase64String(sha.Hash);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error("Can't read file", item.File.FullName, ex.Message);
                return false;
            }
        }

        /// <summary>true = same bytes, false = different, null = can't read or canceled.</summary>
        bool? SameBytes(FileInfo a, FileInfo b, double from, double to)
        {
            try
            {
                using (var s1 = OpenRead(a))
                using (var s2 = OpenRead(b))
                {
                    if (s1.Length != s2.Length) return false;
                    var b1 = new byte[BufferSize];
                    var b2 = new byte[BufferSize];
                    while (true)
                    {
                        if (cancel) return null;
                        int r1 = ReadFull(s1, b1);
                        int r2 = ReadFull(s2, b2);
                        if (r1 != r2) return false;
                        if (r1 == 0) return true;
                        for (int i = 0; i < r1; i++)
                            if (b1[i] != b2[i]) return false;
                        bytes_done += r1;
                        if (bytes_total > 0)
                            SetProgress(from + (to - from) * bytes_done / bytes_total);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        static FileStream OpenRead(FileInfo f)
        {
            return new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        }

        static byte[] ReadExactly(Stream s, int count)
        {
            var buffer = new byte[count];
            int done = 0, read;
            while (done < count && (read = s.Read(buffer, done, count - done)) > 0)
                done += read;
            if (done < count)
                Array.Resize(ref buffer, done);
            return buffer;
        }

        static int ReadFull(Stream s, byte[] buffer)
        {
            int done = 0, read;
            while (done < buffer.Length && (read = s.Read(buffer, done, buffer.Length - done)) > 0)
                done += read;
            return done;
        }

        #endregion

        #region Compare two files

        internal IsSameResult IsSame(FileInfo first, FileInfo second, ScanMode mode)
        {
            Start("Comparing files...");
            log = new ScanLog();
            log.Begin("Compare", new List<string> { first.FullName, second.FullName }, new ScanOptions { Mode = mode });
            IsSameResult result;
            try
            {
                result = Compare(first, second, mode);
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error", "", ex.Message);
                result = IsSameResult.CanNotReadFile;
            }
            log.EndCompare(result);
            persent = 100;
            Indeterminate = false;
            CurrentAction = "Finished.";
            ison = false;
            return result;
        }

        IsSameResult Compare(FileInfo first, FileInfo second, ScanMode mode)
        {
            first.Refresh();
            second.Refresh();
            if (!first.Exists || !second.Exists)
                return IsSameResult.FileIsNotExist;

            log.StepStart("Comparing sizes");
            bool same_size = first.Length == second.Length;
            log.StepEnd(same_size ? "same size" : "different sizes");
            if (!same_size) return IsSameResult.NotSame;

            string id1 = NativeFile.GetFileId(first.FullName), id2 = NativeFile.GetFileId(second.FullName);
            if (id1 != null && id1 == id2)
            {
                log.Skip("Same file (hard link)", first.FullName + "  =  " + second.FullName);
                return IsSameResult.Same;
            }
            if (mode == ScanMode.SizeOnly) return IsSameResult.Same;

            var a = new Duplicate_item(first);
            var b = new Duplicate_item(second);
            log.StepStart("Comparing samples");
            if (!ReadSample(a) || !ReadSample(b)) return IsSameResult.CanNotReadFile;
            bool same_sample = a.FilePeek == b.FilePeek;
            log.StepEnd(same_sample ? "same" : "different");
            SetProgress(5);
            if (!same_sample) return IsSameResult.NotSame;
            if (mode == ScanMode.Sample) return IsSameResult.Same;

            // Two files: comparing the bytes directly is as fast as hashing both, and exact.
            log.StepStart("Byte-by-byte check");
            bytes_done = 0;
            bytes_total = first.Length;
            bool? same = SameBytes(first, second, 5, 100);
            log.StepEnd(same == true ? "same" : same == false ? "different" : "stopped");
            if (cancel) return IsSameResult.Canceled;
            if (same == null) return IsSameResult.CanNotReadFile;
            return same.Value ? IsSameResult.Same : IsSameResult.NotSame;
        }

        #endregion
    }

    /// <summary>Collects what happened during a scan and writes it as a readable text log.</summary>
    class ScanLog
    {
        class Step { public string Name; public DateTime Start; public TimeSpan Time; public string Result; }

        string kind = "";
        List<string> paths = new List<string>();
        ScanOptions options = new ScanOptions();
        DateTime started, finished;
        bool canceled;
        List<Step> steps = new List<Step>();
        List<KeyValuePair<string, string>> skipped = new List<KeyValuePair<string, string>>();
        List<string> errors = new List<string>();
        List<Duplicate_Group> result = new List<Duplicate_Group>();
        IsSameResult? compare_result;

        public int FoldersScanned;
        public int FilesScanned;
        public long BytesScanned;

        public void Begin(string kind, List<string> paths, ScanOptions options)
        {
            this.kind = kind;
            this.paths = paths.ToList();
            this.options = options;
            started = DateTime.Now;
        }

        public void StepStart(string name)
        {
            steps.Add(new Step { Name = name, Start = DateTime.Now });
        }

        public void StepEnd(string result)
        {
            var s = steps[steps.Count - 1];
            s.Time = DateTime.Now - s.Start;
            s.Result = result;
        }

        public void Skip(string reason, string path) { skipped.Add(new KeyValuePair<string, string>(reason, path)); }

        public void Error(string what, string path, string message)
        {
            errors.Add(what + (path.Length > 0 ? ": " + path : "") + (message.Length > 0 ? "  (" + message + ")" : ""));
        }

        public void End(bool canceled, List<Duplicate_Group> result)
        {
            finished = DateTime.Now;
            this.canceled = canceled;
            this.result = result;
        }

        public void EndCompare(IsSameResult result)
        {
            finished = DateTime.Now;
            canceled = result == IsSameResult.Canceled;
            compare_result = result;
        }

        static string ModeName(ScanMode m)
        {
            switch (m)
            {
                case ScanMode.SizeOnly: return "Size only";
                case ScanMode.Sample: return "Fast (size + sampled bytes)";
                case ScanMode.Smart: return "Smart (size + sampled bytes + SHA-512)";
                case ScanMode.Full: return "Full (size + sampled bytes + SHA-512 + byte-by-byte check)";
                default: return m.ToString();
            }
        }

        static string YesNo(bool b) { return b ? "yes" : "no"; }

        public override string ToString()
        {
            if (started == default(DateTime))
                return "";
            var sb = new StringBuilder();
            string title = kind == "Compare" ? "Duplicate Files Cleaner - compare log" : "Duplicate Files Cleaner - scan log";
            sb.AppendLine(title);
            sb.AppendLine(new string('=', title.Length));
            sb.AppendLine("Version   : " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            sb.AppendLine("Started   : " + started.ToString("yyyy-MM-dd HH:mm:ss"));
            if (finished != default(DateTime))
            {
                sb.AppendLine("Finished  : " + finished.ToString("yyyy-MM-dd HH:mm:ss") + (canceled ? "  (canceled)" : ""));
                sb.AppendLine("Duration  : " + Duration(finished - started));
            }
            sb.AppendLine();

            Section(sb, "Settings");
            if (kind == "Compare")
            {
                sb.AppendLine("File 1    : " + paths[0]);
                sb.AppendLine("File 2    : " + paths[1]);
                sb.AppendLine("Scan mode : " + ModeName(options.Mode));
            }
            else
            {
                sb.AppendLine("Folders   : " + string.Join("  |  ", paths.ToArray()));
                sb.AppendLine("Scan mode : " + ModeName(options.Mode));
                sb.AppendLine("Skip hidden files and folders : " + YesNo(options.SkipHidden));
                sb.AppendLine("Skip system files and folders : " + YesNo(options.SkipSystem));
                sb.AppendLine("Skip saved web page folders   : " + YesNo(options.SkipWebPageFolders));
                sb.AppendLine("Excluded folders              : " + (options.ExcludedFolders.Count == 0 ? "none" : ""));
                foreach (var f in options.ExcludedFolders)
                    sb.AppendLine("    " + f);
            }
            sb.AppendLine();

            Section(sb, "Summary");
            if (kind == "Compare")
            {
                string r;
                switch (compare_result)
                {
                    case IsSameResult.Same: r = "The files are identical."; break;
                    case IsSameResult.NotSame: r = "The files are different."; break;
                    case IsSameResult.FileIsNotExist: r = "A file was not found."; break;
                    case IsSameResult.CanNotReadFile: r = "A file could not be read."; break;
                    case IsSameResult.Canceled: r = "Canceled."; break;
                    default: r = "Unknown."; break;
                }
                sb.AppendLine("Result    : " + r);
            }
            else
            {
                long extra = result.Sum(g => g.Size * (g.Items.Count - 1));
                sb.AppendLine(string.Format("Folders read     : {0:N0}", FoldersScanned));
                sb.AppendLine(string.Format("Files read       : {0:N0} ({1})", FilesScanned, Functions.FileSize(BytesScanned, 2)));
                if (!canceled)
                {
                    sb.AppendLine(string.Format("Duplicate groups : {0:N0}", result.Count));
                    sb.AppendLine(string.Format("Duplicate files  : {0:N0} (extra copies: {1:N0}, {2})", result.Sum(g => g.Items.Count), result.Sum(g => g.Items.Count - 1), Functions.FileSize(extra, 2)));
                }
            }
            sb.AppendLine(string.Format("Skipped          : {0:N0}", skipped.Count));
            sb.AppendLine(string.Format("Errors           : {0:N0}", errors.Count));
            sb.AppendLine();

            Section(sb, "Steps");
            foreach (var s in steps)
                sb.AppendLine(string.Format("{0}  {1,-22} {2}  {3}", s.Start.ToString("HH:mm:ss"), s.Name, Duration(s.Time), s.Result ?? "stopped"));
            sb.AppendLine();

            if (errors.Count > 0)
            {
                Section(sb, "Errors");
                foreach (var e in errors)
                    sb.AppendLine(e);
                sb.AppendLine();
            }

            if (skipped.Count > 0)
            {
                Section(sb, "Skipped");
                foreach (var reason in skipped.GroupBy(s => s.Key))
                {
                    sb.AppendLine(string.Format("{0} ({1:N0})", reason.Key, reason.Count()));
                    foreach (var s in reason)
                        sb.AppendLine("    " + s.Value);
                }
                sb.AppendLine();
            }

            if (kind != "Compare" && !canceled && result.Count > 0)
            {
                Section(sb, "Duplicates");
                int n = 0;
                foreach (var g in result.OrderByDescending(g => g.Size * (g.Items.Count - 1)))
                {
                    sb.AppendLine(string.Format("#{0}  {1} x {2} copies", ++n, Functions.FileSize(g.Size, 2), g.Items.Count));
                    foreach (var i in g.Items)
                        sb.AppendLine("    " + i.File.FullName);
                }
            }
            return sb.ToString();
        }

        static void Section(StringBuilder sb, string name)
        {
            sb.AppendLine(name);
            sb.AppendLine(new string('-', name.Length));
        }

        static string Duration(TimeSpan t)
        {
            return string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
        }
    }

    static class NativeFile
    {
        [StructLayout(LayoutKind.Sequential)]
        struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
            public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string name, uint access, FileShare share, IntPtr security, FileMode mode, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION info);

        const uint FILE_READ_ATTRIBUTES = 0x80;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        /// <summary>The volume serial and file index: equal for two paths of the same file (hard links). null if unknown.</summary>
        public static string GetFileId(string path)
        {
            using (var h = CreateFile(path, FILE_READ_ATTRIBUTES, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                BY_HANDLE_FILE_INFORMATION info;
                if (!GetFileInformationByHandle(h, out info)) return null;
                return info.VolumeSerialNumber.ToString("X8") + ":" + info.FileIndexHigh.ToString("X8") + info.FileIndexLow.ToString("X8");
            }
        }
    }

    class Duplicate_Group
    {
        public long Size { get; set; }
        public List<Duplicate_item> Items = new List<Duplicate_item>();

        public Duplicate_Group(long size)
        {
            Size = size;
        }
    }

    class Duplicate_item
    {
        public FileInfo File { get; set; }
        public string FilePeek { get; set; }
        public string Hashes { get; set; }

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
