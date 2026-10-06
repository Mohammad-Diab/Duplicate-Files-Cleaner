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
    /// Scan modes. The numbers are the values saved in Settings.scan_quality.
    /// SizeOnly and Sample can call different files duplicates, so their results are only a preview.
    /// </summary>
    enum ScanMode
    {
        SizeOnly = 1,   // preview: same size
        Sample = 2,     // preview: same size + same bytes at sampled positions
        Smart = 3,      // same SHA-512 fingerprint of the whole file
        Full = 4        // same bytes, compared directly (exact)
    }

    class ScanOptions
    {
        public ScanMode Mode = ScanMode.Full;
        public bool SkipHidden = true;
        public bool SkipSystem = true;
        public bool SkipGenerated = true;
        public List<string> GeneratedFolderNames = new List<string>();
        public List<string> ExcludedFolders = new List<string>();

        public bool IsPreview { get => Mode == ScanMode.SizeOnly || Mode == ScanMode.Sample; }
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
            Indeterminate = false;
            CurrentAction = action;
            if (BufferSize < 1024)
                BufferSize = 32768;
        }

        void SetProgress(double value)
        {
            Indeterminate = false;
            persent = Math.Max(persent, Math.Max(0, Math.Min(100, value)));   // never goes back
        }

        #region Steps and progress

        // Every step owns a part of the progress bar, at least 10%, so short steps are still visible.
        string[] step_names = new string[0];
        double[] step_weights = new double[0];
        int step = -1;
        double step_start;

        void BeginSteps(string[] names, double[] weights)
        {
            step_names = names;
            step_weights = weights;
            step = -1;
            step_start = 0;
        }

        /// <summary>Gives the steps after the current one new weights (they keep their total), each at least 10%.</summary>
        void ReweighRemaining(double[] costs)
        {
            int first = step + 1;
            double left = 0;
            for (int i = first; i < step_weights.Length; i++) left += step_weights[i];
            int n = step_weights.Length - first;
            if (n <= 0 || costs.Length != n) return;
            double min = 10, total_cost = costs.Sum();
            double free = left - min * n;
            for (int i = 0; i < n; i++)
                step_weights[first + i] = min + (total_cost > 0 ? free * costs[i] / total_cost : free / n);
        }

        void EnterStep(int index, string detail = "")
        {
            step_start = step_weights.Take(index).Sum();
            step = index;
            SetProgress(step_start);
            SetStepText(detail);
        }

        void StepProgress(double fraction)
        {
            if (step < 0) return;
            SetProgress(step_start + step_weights[step] * Math.Max(0, Math.Min(1, fraction)));
        }

        void SetStepText(string detail)
        {
            CurrentAction = string.Format("Step {0} of {1}: {2}{3}", step + 1, step_names.Length, step_names[step], detail.Length > 0 ? " - " + detail : "...");
        }

        #endregion

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
            Finish();
            return cancel ? new List<Duplicate_Group>() : result;
        }

        /// <summary>Runs the exact check on the groups of a preview scan. Returns only the real duplicates.</summary>
        public List<Duplicate_Group> Confirm(List<Duplicate_Group> groups)
        {
            Start("Checking files byte by byte...");
            BeginSteps(new[] { "Exact check (byte by byte)" }, new double[] { 100 });
            var result = new List<Duplicate_Group>();
            try
            {
                EnterStep(0);
                log.StepStart("Exact check of the preview");
                result = Sort(CompareExact(groups));
                log.StepEnd(string.Format("{0:N0} groups confirmed", result.Count));
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error", "", ex.Message);
                result.Clear();
            }
            log.End(cancel, result, true);
            Finish();
            return cancel ? null : result;
        }

        void Finish()
        {
            persent = 100;
            Indeterminate = false;
            CurrentAction = cancel ? "Canceled." : "Finished.";
            ison = false;
        }

        List<Duplicate_Group> Find(List<string> paths, ScanOptions options)
        {
            switch (options.Mode)
            {
                case ScanMode.SizeOnly:
                    BeginSteps(new[] { "Reading folders", "Checking sizes and hard links" }, new double[] { 80, 20 });
                    break;
                case ScanMode.Sample:
                    BeginSteps(new[] { "Reading folders", "Checking sizes and hard links", "Comparing samples" }, new double[] { 40, 10, 50 });
                    break;
                case ScanMode.Smart:
                    BeginSteps(new[] { "Reading folders", "Checking sizes and hard links", "Comparing samples", "Hashing files (SHA-512)" }, new double[] { 20, 10, 35, 35 });
                    break;
                default:
                    BeginSteps(new[] { "Reading folders", "Checking sizes and hard links", "Comparing samples", "Comparing files byte by byte" }, new double[] { 20, 10, 35, 35 });
                    break;
            }

            // Step 1: read the folders and group the files by size.
            EnterStep(0);
            log.StepStart("Reading folders");
            var excluded = new HashSet<string>(options.ExcludedFolders.Select(NormalizeFolder), StringComparer.OrdinalIgnoreCase);
            var generated = new HashSet<string>(options.SkipGenerated ? options.GeneratedFolderNames : new List<string>(), StringComparer.OrdinalIgnoreCase);
            var by_size = new Dictionary<long, Duplicate_Group>();
            read_fraction = 0;
            foreach (var path in paths)
            {
                ReadFolder(path, 1.0 / paths.Count, options, excluded, generated, by_size);
                if (cancel) return new List<Duplicate_Group>();
            }
            log.StepEnd(string.Format("{0:N0} files, {1}", log.FilesScanned, Functions.FileSize(log.BytesScanned, 2)));

            // Step 2: keep files that have the same size as another file. The same file reached through two paths
            // (hard links, or a folder chosen twice through a junction) is one file, not a duplicate.
            EnterStep(1);
            log.StepStart("Checking sizes and hard links");
            var groups = by_size.Values.Where(g => g.Items.Count > 1).ToList();
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
                StepProgress((g + 1.0) / groups.Count);
            }
            groups = groups.Where(g => g.Items.Count > 1).ToList();
            log.StepEnd(string.Format("{0:N0} files in {1:N0} same-size groups; {2}", groups.Sum(g => g.Items.Count), groups.Count,
                links == 0 ? "no hard links" : string.Format("{0:N0} hard link{1} ignored", links, links == 1 ? "" : "s")));

            if (options.Mode == ScanMode.SizeOnly || groups.Count == 0)
                return Sort(groups);

            // The last two steps share their part of the bar by their expected cost: samples by the number of
            // files (a few small reads each), the full read by the bytes to read.
            if (options.Mode != ScanMode.Sample)
            {
                double sample_cost = groups.Sum(g => g.Items.Count) * 0.003;              // ~3 ms per file
                double read_cost = groups.Sum(g => (double)g.Size * g.Items.Count) / 150e6; // ~150 MB/s
                ReweighRemaining(new[] { sample_cost, read_cost });
            }

            // Step 3: compare a sample of bytes from each file. Files of 4 KiB or less are read whole.
            EnterStep(2);
            log.StepStart("Comparing samples");
            int total_items = groups.Sum(g => g.Items.Count), done_items = 0;
            foreach (var group in groups)
                foreach (var item in group.Items.ToList())
                {
                    if (cancel) return new List<Duplicate_Group>();
                    if (!ReadSample(item))
                        group.Items.Remove(item);
                    StepProgress(++done_items / (double)total_items);
                    if (done_items % 50 == 0)
                        SetStepText(string.Format("{0:N0} of {1:N0} files", done_items, total_items));
                }
            groups = Split(groups, i => i.FilePeek);
            log.StepEnd(string.Format("{0:N0} groups left", groups.Count));

            if (options.Mode == ScanMode.Sample || groups.Count == 0)
                return Sort(groups);

            EnterStep(3);
            if (options.Mode == ScanMode.Smart)
            {
                // Step 4 (Smart): SHA-512 of the whole content.
                log.StepStart("Hashing (SHA-512)");
                bytes_done = 0;
                bytes_total = groups.Sum(g => g.Items.Where(i => i.File.Length > SampleWholeFileLimit).Sum(i => i.File.Length));
                foreach (var group in groups)
                    foreach (var item in group.Items.ToList())
                    {
                        if (cancel) return new List<Duplicate_Group>();
                        if (!Hash(item))
                            group.Items.Remove(item);
                    }
                if (cancel) return new List<Duplicate_Group>();
                groups = Split(groups, i => i.Hashes);
                log.StepEnd(string.Format("{0:N0} groups left", groups.Count));
                return Sort(groups);
            }

            // Step 4 (Full): compare the bytes directly, no hashing.
            log.StepStart("Byte-by-byte check");
            groups = CompareExact(groups.Where(g => g.Size > SampleWholeFileLimit).ToList())
                .Concat(groups.Where(g => g.Size <= SampleWholeFileLimit)).ToList();   // small files were compared whole
            log.StepEnd(string.Format("{0:N0} groups confirmed", groups.Count));
            return Sort(groups);
        }

        /// <summary>
        /// Exact comparison: the files of a group are read together, piece by piece, and split as soon as their
        /// pieces differ. Each file is read once, and a file that differs early is not read further.
        /// </summary>
        List<Duplicate_Group> CompareExact(List<Duplicate_Group> groups)
        {
            var result = new List<Duplicate_Group>();
            bytes_done = 0;
            bytes_total = groups.Sum(g => g.Size * g.Items.Count);
            var piece = new byte[BufferSize];
            foreach (var group in groups)
            {
                if (cancel) return result;
                var streams = new Dictionary<Duplicate_item, FileStream>();
                try
                {
                    var alive = new List<Duplicate_item>();
                    foreach (var item in group.Items)
                    {
                        try
                        {
                            var s = OpenRead(item.File);
                            if (s.Length != group.Size) { s.Dispose(); throw new IOException("The file changed after it was found."); }
                            streams.Add(item, s);
                            alive.Add(item);
                        }
                        catch (Exception ex)
                        {
                            log.Error("Can't read file", item.File.FullName, ex.Message);
                            bytes_done += group.Size;
                        }
                    }

                    var parts = new List<List<Duplicate_item>>();
                    if (alive.Count > 1) parts.Add(alive);
                    else bytes_done += group.Size * alive.Count;
                    long offset = 0;
                    while (offset < group.Size && parts.Count > 0)
                    {
                        int length = (int)Math.Min(piece.Length, group.Size - offset);
                        var next = new List<List<Duplicate_item>>();
                        foreach (var part in parts)
                        {
                            // One bucket per different content of this piece; the first file's bytes stand for it.
                            var bucket_bytes = new List<byte[]>();
                            var buckets = new List<List<Duplicate_item>>();
                            foreach (var item in part)
                            {
                                if (cancel) return result;
                                try
                                {
                                    if (ReadFull(streams[item], piece, length) != length)
                                        throw new IOException("The file changed while it was read.");
                                }
                                catch (Exception ex)
                                {
                                    log.Error("Can't read file", item.File.FullName, ex.Message);
                                    bytes_done += group.Size - offset;
                                    continue;
                                }
                                bytes_done += length;
                                int b = 0;
                                while (b < buckets.Count && !SameBytes(bucket_bytes[b], piece, length)) b++;
                                if (b == buckets.Count)
                                {
                                    var copy = new byte[length];
                                    Buffer.BlockCopy(piece, 0, copy, 0, length);
                                    bucket_bytes.Add(copy);
                                    buckets.Add(new List<Duplicate_item>());
                                }
                                buckets[b].Add(item);
                            }
                            foreach (var bucket in buckets)
                                if (bucket.Count > 1)
                                    next.Add(bucket);
                                else
                                    bytes_done += group.Size - offset - length;   // a different file is not read further
                        }
                        parts = next;
                        offset += length;
                        if (bytes_total > 0)
                        {
                            StepProgress((double)bytes_done / bytes_total);
                            SetStepText(string.Format("{0} of {1}", Functions.FileSize(bytes_done, 1), Functions.FileSize(bytes_total, 1)));
                        }
                    }
                    foreach (var part in parts)
                    {
                        var g = new Duplicate_Group(group.Size);
                        g.Items.AddRange(part);
                        result.Add(g);
                    }
                }
                finally
                {
                    foreach (var s in streams.Values)
                        s.Dispose();
                }
            }
            return result;
        }

        static bool SameBytes(byte[] a, byte[] b, int length)
        {
            for (int i = 0; i < length; i++)
                if (a[i] != b[i]) return false;
            return true;
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

        class FolderWork
        {
            public DirectoryInfo Dir;
            public double Share;   // part of the "Reading folders" step this folder and its subfolders stand for
        }

        double read_fraction;

        void ReadFolder(string root, double share, ScanOptions options, HashSet<string> excluded, HashSet<string> generated, Dictionary<long, Duplicate_Group> by_size)
        {
            if (!Directory.Exists(root))
            {
                log.Error("Folder not found", root, "");
                read_fraction += share;
                return;
            }
            // A folder the user chose is always read, even when it is a junction or a hidden or system folder.
            // Inside it, junctions and symbolic links are never followed, so a link can't cause a loop.
            // The total number of folders is unknown, so each folder's share of the step is split evenly
            // between its own files and its subfolders.
            var stack = new Stack<FolderWork>();
            stack.Push(new FolderWork { Dir = new DirectoryInfo(root), Share = share });
            while (stack.Count > 0)
            {
                if (cancel) return;
                var work = stack.Pop();
                var dir = work.Dir;
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
                    read_fraction += work.Share;
                    continue;
                }
                log.FoldersScanned++;

                var to_read = new List<DirectoryInfo>();
                foreach (var d in dirs)
                {
                    string reason = FolderSkipReason(d, files, options, excluded, generated);
                    if (reason == null)
                        to_read.Add(d);
                    else
                        log.Skip(reason, d.FullName);
                }
                double part = work.Share / (to_read.Count + 1);
                foreach (var d in to_read)
                    stack.Push(new FolderWork { Dir = d, Share = part });

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
                read_fraction += part;
                StepProgress(read_fraction);
                SetStepText(string.Format("{0:N0} files found", log.FilesScanned));
            }
        }

        static string FolderSkipReason(DirectoryInfo d, FileInfo[] files_beside, ScanOptions options, HashSet<string> excluded, HashSet<string> generated)
        {
            var a = d.Attributes;
            if ((a & FileAttributes.ReparsePoint) != 0) return "Junction or folder link (not followed)";
            if (options.SkipHidden && (a & FileAttributes.Hidden) != 0) return "Hidden folder";
            if (options.SkipSystem && (a & FileAttributes.System) != 0) return "System folder";
            if (excluded.Contains(NormalizeFolder(d.FullName))) return "Excluded folder";
            if (generated.Contains(d.Name)) return "Project or generated folder";
            if (options.SkipGenerated && IsWebPageFolder(d, files_beside)) return "Saved web page folder";
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

        bool Hash(Duplicate_item item)
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
                        {
                            StepProgress((double)bytes_done / bytes_total);
                            SetStepText(string.Format("{0} of {1}", Functions.FileSize(bytes_done, 1), Functions.FileSize(bytes_total, 1)));
                        }
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
            return ReadFull(s, buffer, buffer.Length);
        }

        static int ReadFull(Stream s, byte[] buffer, int count)
        {
            int done = 0, read;
            while (done < count && (read = s.Read(buffer, done, count - done)) > 0)
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
        bool confirmed;

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

        public void End(bool canceled, List<Duplicate_Group> result, bool confirmation = false)
        {
            finished = DateTime.Now;
            this.canceled = canceled;
            if (confirmation && canceled) return;   // a canceled confirmation keeps the preview result
            this.result = result;
            if (confirmation) confirmed = true;
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
                case ScanMode.SizeOnly: return "Size preview (same size only; not confirmed)";
                case ScanMode.Sample: return "Quick preview (size + sampled bytes; not confirmed)";
                case ScanMode.Smart: return "Fingerprint (size + sampled bytes + SHA-512 of the whole file)";
                case ScanMode.Full: return "Exact (size + sampled bytes + byte-by-byte comparison)";
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
                if (options.IsPreview)
                    sb.AppendLine(confirmed ? "            The preview was confirmed by the exact check." : "            Preview: the groups below may include files that are not duplicates.");
                sb.AppendLine("Skip hidden files and folders : " + YesNo(options.SkipHidden));
                sb.AppendLine("Skip system files and folders : " + YesNo(options.SkipSystem));
                sb.AppendLine("Skip project/generated folders: " + (options.SkipGenerated
                    ? "yes (" + string.Join(", ", options.GeneratedFolderNames.ToArray()) + ", saved web page folders)" : "no"));
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
                sb.AppendLine(string.Format("{0}  {1,-30} {2}  {3}", s.Start.ToString("HH:mm:ss"), s.Name, Duration(s.Time), s.Result ?? "stopped"));
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
