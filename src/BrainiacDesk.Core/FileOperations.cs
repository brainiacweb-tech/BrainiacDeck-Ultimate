using System.IO.Compression;
using System.Security.Cryptography;
namespace BrainiacDesk.Core;
public sealed record RenameEntry(string Source, string Destination);
public static class FileOperations
{
    public static async Task<string> HashAsync(string path, bool sha512 = false, CancellationToken token = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(sha512 ? await SHA512.HashDataAsync(stream, token) : await SHA256.HashDataAsync(stream, token));
    }
    public static async Task<IReadOnlyList<string[]>> DuplicatesAsync(string folder, CancellationToken token, IProgress<string>? progress = null)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
        var files = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(folder, "*", options)) { token.ThrowIfCancellationRequested(); files.Add(new(path)); if (files.Count > 100000) throw new IOException("Scan limit is 100,000 files. Select a smaller folder."); }
        var result = new List<string[]>();
        foreach (var sizeGroup in files.GroupBy(x => x.Length).Where(g => g.Count() > 1))
        {
            var groups = new Dictionary<string, List<string>>();
            foreach (var file in sizeGroup) { token.ThrowIfCancellationRequested(); progress?.Report(file.FullName); var hash = await HashAsync(file.FullName, false, token); if (!groups.TryGetValue(hash, out var group)) groups[hash] = group = []; group.Add(file.FullName); }
            foreach (var group in groups.Values.Where(x => x.Count > 1))
            {
                // Verify bytes after hashing; changed or unreadable files fail the scan instead of being called duplicates.
                var verified = new List<string> { group[0] };
                foreach (var candidate in group.Skip(1)) if (await EqualAsync(group[0], candidate, token)) verified.Add(candidate);
                if (verified.Count > 1) result.Add(verified.ToArray());
            }
        }
        return result;
    }
    private static async Task<bool> EqualAsync(string first, string second, CancellationToken token)
    {
        await using var a = File.OpenRead(first); await using var b = File.OpenRead(second);
        if (a.Length != b.Length) return false;
        var left = new byte[65536]; var right = new byte[65536];
        while (true) { token.ThrowIfCancellationRequested(); var n = await a.ReadAsync(left, token); if (n == 0) return b.Position == b.Length; await b.ReadExactlyAsync(right.AsMemory(0, n), token); if (!left.AsSpan(0, n).SequenceEqual(right.AsSpan(0, n))) return false; }
    }
    public static RenameEntry[] PlanRename(string[] files, string prefix, string suffix, int start)
    {
        if (prefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Prefix/suffix contains invalid filename characters.");
        var plan = files.Select((source, index) => new RenameEntry(Path.GetFullPath(source), Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!, prefix + (start + index).ToString("D3") + suffix + Path.GetExtension(source)))).ToArray();
        if (plan.Select(x => x.Destination).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Length) throw new IOException("Rename destinations collide.");
        foreach (var item in plan) { if (!File.Exists(item.Source)) throw new FileNotFoundException("Source no longer exists.", item.Source); if (!item.Source.Equals(item.Destination, StringComparison.OrdinalIgnoreCase) && (File.Exists(item.Destination) || Directory.Exists(item.Destination))) throw new IOException("Destination already exists: " + item.Destination); }
        return plan;
    }
    public static void Rename(RenameEntry[] plan)
    {
        var completed = new List<RenameEntry>();
        try { foreach (var item in plan) { if (item.Source.Equals(item.Destination, StringComparison.OrdinalIgnoreCase)) continue; File.Move(item.Source, item.Destination, false); completed.Add(item); } }
        catch (Exception original)
        {
            var failures = new List<Exception> { original }; foreach (var item in completed.AsEnumerable().Reverse()) { try { File.Move(item.Destination, item.Source, false); } catch (Exception ex) { failures.Add(ex); } }
            throw new AggregateException("Rename interrupted; attempted rollback. Inspect listed paths if rollback also failed.", failures);
        }
    }
    public static async Task CreateZipAsync(string[] inputs, string output, CancellationToken token)
    {
        if (inputs.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Length) throw new IOException("ZIP entries would have duplicate names.");
        var temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
                foreach (var input in inputs) { token.ThrowIfCancellationRequested(); var entry = archive.CreateEntry(Path.GetFileName(input), CompressionLevel.Optimal); await using var destination = entry.Open(); await using var source = File.OpenRead(input); await source.CopyToAsync(destination, token); }
            }
            token.ThrowIfCancellationRequested(); File.Move(temp, output, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string SafeArchivePath(string root, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.Contains(':') || entry.Contains('\\') || Path.IsPathRooted(entry)) throw new InvalidDataException("Unsafe archive entry.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(fullRoot, entry));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escapes destination.");
        return target;
    }
    public static async Task ExtractZipAsync(string input, string output, CancellationToken token)
    {
        // Always create an isolated directory. Never extract into an existing directory or follow existing links.
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Extraction destination must not exist.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(output))!;
        for (var current = new DirectoryInfo(parent); current is not null; current = current.Parent) if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Extraction through linked directories is not allowed.");
        var temp = Path.Combine(parent, ".brainiac-extract-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            using var archive = ZipFile.OpenRead(input); long total = 0;
            if (archive.Entries.Count > 5000) throw new InvalidDataException("Archive exceeds 5000 entries.");
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested(); total = checked(total + entry.Length);
                if (total > 2_000_000_000 || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Archive exceeds size limit or contains symbolic links.");
                var destination = SafeArchivePath(temp, entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = entry.Open(); await using var target = new FileStream(destination, FileMode.CreateNew);
                var buffer = new byte[65536]; long written = 0; int count;
                while ((count = await source.ReadAsync(buffer, token)) > 0) { written += count; if (written > entry.Length) throw new InvalidDataException("Archive expanded beyond declared size."); await target.WriteAsync(buffer.AsMemory(0, count), token); }
            }
            token.ThrowIfCancellationRequested(); Directory.Move(temp, output);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
}