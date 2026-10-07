using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework;

namespace FolderSync.Tests;

/// <summary>
/// Black-box tests: every test runs the program through Program.Main(args),
/// </summary>
[TestFixture]
public class SyncTests
{
    private string _root = null!;
    private string _source = null!;
    private string _replica = null!;
    private string _log = null!;
    private const int timeout = 3;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "foldersync_" + Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _replica = Path.Combine(_root, "replica");
        _log = Path.Combine(_root, "logs", "sync.log");
        Directory.CreateDirectory(_source);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ---------- helpers ----------

    private int Run(int interval = 1, int timeoutSeconds = timeout)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return Program.MainImpl(new[] { _source, _replica, interval.ToString(), _log }, cts.Token);
    }

    private static void Write(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static string[] Tree(string root) =>
        Directory
            .EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

    private void AssertReplicaMatchesSource()
    {
        Assert.That(Tree(_replica), Is.EqualTo(Tree(_source)), "folder structure differs");
        foreach (var rel in Tree(_source).Where(r => File.Exists(Path.Combine(_source, r))))
        {
            Assert.That(
                File.ReadAllBytes(Path.Combine(_replica, rel)),
                Is.EqualTo(File.ReadAllBytes(Path.Combine(_source, rel))),
                $"content differs: {rel}"
            );
        }
    }

    // ---------- implemented examples ----------

    [Test]
    public void Sync_CreatesReplicaFolder_WhenItDoesNotExist()
    {
        Write(_source, "a.txt", "hello");

        Assert.That(Run(), Is.EqualTo(0));

        Assert.That(Directory.Exists(_replica));
        AssertReplicaMatchesSource();
    }

    [Test]
    public void Sync_CopiesNestedFilesAndFolders()
    {
        Write(_source, "a.txt", "a");
        Write(_source, Path.Combine("dir1", "b.txt"), "b");
        Write(_source, Path.Combine("dir1", "dir2", "c.txt"), "c");

        Run();

        AssertReplicaMatchesSource();
    }

    [Test]
    public void Sync_ReplicatesEmptyFolders()
    {
        Directory.CreateDirectory(Path.Combine(_source, "empty", "nested"));

        Run();

        Assert.That(Directory.Exists(Path.Combine(_replica, "empty", "nested")));
    }

    [Test]
    public void Sync_OverwritesChangedFile_EvenWhenSizeIsTheSame()
    {
        Write(_source, "a.txt", "AAAA");
        Write(_replica, "a.txt", "BBBB"); // same length, different content

        Run();

        Assert.That(File.ReadAllText(Path.Combine(_replica, "a.txt")), Is.EqualTo("AAAA"));
    }

    [Test]
    public void Sync_RemovesFilesAndFoldersThatAreNotInSource()
    {
        Write(_source, "keep.txt", "keep");
        Write(_replica, "keep.txt", "keep");
        Write(_replica, "extra.txt", "x");
        Write(_replica, Path.Combine("extra_dir", "deep", "y.txt"), "y");

        Run();

        AssertReplicaMatchesSource();
        Assert.That(File.Exists(Path.Combine(_replica, "extra.txt")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_replica, "extra_dir")), Is.False);
    }

    [Test]
    public void Sync_WritesLogFile_WithOneEntryPerOperation()
    {
        Write(_source, "new.txt", "n");
        Write(_replica, "old.txt", "o");

        Run();

        var log = File.ReadAllText(_log);
        Assert.That(log, Does.Contain("new.txt"), "copy of new.txt should be logged");
        Assert.That(log, Does.Contain("old.txt"), "removal of old.txt should be logged");
    }

    [Test]
    public void Main_ReturnsNonZeroAndDoesNotTouchReplica_WhenArgumentsAreMissing()
    {
        Write(_source, "a.txt", "a");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        var code = Program.MainImpl([_source, _replica, "1"], cts.Token); // log path missing

        Assert.That(code, Is.Not.EqualTo(0));
        Assert.That(Directory.Exists(_replica), Is.False);
    }

    // ---------- TASKS ----------
    //
    // ARGUMENTS
    // [ ] Interval = 0 or negative  -> non-zero exit code, replica untouched
    // [ ] Interval is not a number ("abc", "1.5", "")
    // [ ] Source folder does not exist
    // [ ] Replica inside source (and source inside replica) is rejected
    // [ ] Arguments in the wrong order are not silently accepted (e.g. numbers in path slots)
    //
    // SYNC BEHAVIOUR
    // [ ] Idempotency: second run on an already-synced pair changes nothing (compare LastWriteTime of replica files)
    // [ ] Source is never modified by a sync (snapshot tree + content before/after)
    // [ ] File in source, directory with the same name in replica (and the reverse)
    // [ ] Names with spaces, unicode and dots; file without extension; zero-byte file
    // [ ] Large file (e.g. 50 MB) syncs correctly and in reasonable time
    // [ ] Read-only file in replica gets overwritten or removed
    // [ ] One locked/unreadable file does not stop the other files from syncing (open it with FileShare.None)
    //
    // LOGGING
    // [ ] Log is appended, not overwritten, across two program runs
    // [ ] Log directory that does not exist yet gets created
    // [ ] Console output contains the same operations as the log (redirect with Console.SetOut)
    // [ ] Nothing is logged for files that are already identical
}
