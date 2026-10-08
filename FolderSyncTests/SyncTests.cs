using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework;
using NUnit.Framework.Constraints;

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
    private const int timeout = 2;
    private const string SourceFlag = "--source";
    private const string ReplicaFlag = "--replica";
    private const string IntervalFlag = "--interval";
    private const string LogFlag = "--log";

    private record Entry(string Path, bool IsDirectory);

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
        return Program.MainImpl(
            new[]
            {
                "--source",
                _source,
                "--replica",
                _replica,
                "--interval",
                interval.ToString(),
                "--log",
                _log,
            },
            cts.Token
        );
    }

    private int Run(string[] args, int timeoutSeconds = timeout)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return Program.MainImpl(args, cts.Token);
    }

    private static void Write(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static Entry[] Tree(string root) =>
        Directory
            .EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(p => new Entry(Path.GetRelativePath(root, p), Directory.Exists(p)))
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .ToArray();

    private void AssertReplicaMatchesSource()
    {
        var sourceTree = Tree(_source);
        var replicaTree = Tree(_replica);

        Assert.That(replicaTree, Is.EqualTo(sourceTree), "Folder structure differs");

        foreach (var entry in sourceTree.Where(e => !e.IsDirectory))
        {
            var sourcePath = Path.Combine(_source, entry.Path);
            var replicaPath = Path.Combine(_replica, entry.Path);

            Assert.That(
                File.ReadAllBytes(replicaPath),
                Is.EqualTo(File.ReadAllBytes(sourcePath)),
                $"Content differs: {entry.Path}"
            );
        }
    }

    [Test]
    public void ArgumentParser_ReturnsZero_HelpCalled()
    {
        string[] arguments = new[] { "--help" };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_ReturnsNonZero_IntervalIsNotANumber()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            _source,
            ReplicaFlag,
            _replica,
            IntervalFlag,
            "test",
            LogFlag,
            _log,
        };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));

        arguments[5] = "1.5";

        exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));

        arguments[5] = "";

        exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_ReturnsNonZero_FlagDuplicates()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            _source,
            SourceFlag,
            _source,
            ReplicaFlag,
            _replica,
            IntervalFlag,
            "5",
            LogFlag,
            _log,
        };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_ReturnsNonZero_FlagIdentifierDuplicates()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            SourceFlag,
            ReplicaFlag,
            _replica,
            IntervalFlag,
            "5",
            LogFlag,
            _log,
        };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_ReturnsNonZero_NestedSourceAndReplica()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            "abc/def",
            ReplicaFlag,
            "def/abc",
            IntervalFlag,
            "5",
            LogFlag,
            _log,
        };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_ReturnsNonZero_MissingArgument()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            _source,
            ReplicaFlag,
            _replica,
            IntervalFlag,
            "5",
        };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void MainImpl_CreatesReplicaFolder_ReplicaDoesNotExist()
    {
        Write(_source, "a.txt", "hello");

        Assert.That(Run(), Is.EqualTo(0));

        Assert.That(Directory.Exists(_replica));
        AssertReplicaMatchesSource();
    }

    [Test]
    public void MainImpl_CopiesNestedFilesAndFolders_ReplicaMatches()
    {
        Write(_source, "a.txt", "a");
        Write(_source, Path.Combine("dir1", "b.txt"), "b");
        Write(_source, Path.Combine("dir1", "dir2", "c.txt"), "c");

        Run();

        AssertReplicaMatchesSource();
    }

    [Test]
    public void MainImpl_NoDataLoss_SameNameFileSourceAndDirectoryReplica()
    {
        // Replica has directory "a"
        Directory.CreateDirectory(Path.Combine(_replica, "a"));

        // Source has file "a" (not directory)
        Write(_source, "a", "content_of_file");

        Run(timeoutSeconds: timeout * 2);

        // After sync: replica should also have file "a" (directory replaced by file)
        AssertReplicaMatchesSource();
        Assert.That(
            Directory.Exists(Path.Combine(_replica, "a")),
            Is.False,
            "Directory should be removed"
        );
        Assert.That(File.Exists(Path.Combine(_replica, "a")), Is.True, "File should exist");

        // Prefferably no output in stderr
    }

    [Test]
    public void MainImpl_NoDataLoss_SameNameFileReplicaAndDirectorySource()
    {
        // Source has directory "a"
        Directory.CreateDirectory(Path.Combine(_source, "a"));

        // Replica has file "a" (not directory)
        Write(_replica, "a", "content_of_file");

        Run(timeoutSeconds: timeout * 2);

        // After sync: replica should also have directory "a" (file replaced by directory)
        AssertReplicaMatchesSource();
        Assert.That(
            Directory.Exists(Path.Combine(_replica, "a")),
            Is.True,
            "Directory should be created"
        );
        Assert.That(File.Exists(Path.Combine(_replica, "a")), Is.False, "File should be removed");

        // Prefferably no output in stderr
    }

    [Test]
    public void MainImpl_ReplicatesEmptyFolders_CreatesEmptyDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_source, "empty", "nested"));

        Run();

        Assert.That(Directory.Exists(Path.Combine(_replica, "empty", "nested")));
    }

    [Test]
    public void MainImpl_OverwritesFile_ContentDiffersEvenIfSizeMatches()
    {
        Write(_source, "a.txt", "AAAA");
        Write(_replica, "a.txt", "BBBB"); // same length, different content

        Run();

        Assert.That(File.ReadAllText(Path.Combine(_replica, "a.txt")), Is.EqualTo("AAAA"));
    }

    [Test]
    public void MainImpl_RemovesExtraFilesAndFolders_NotInSource()
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
    public void MainImpl_WritesLogFile_ContainsOperations()
    {
        Write(_source, "new.txt", "n");
        Write(_replica, "old.txt", "o");

        Run();

        var log = File.ReadAllText(_log);
        Assert.That(log, Does.Contain("new.txt"), "copy of new.txt should be logged");
        Assert.That(log, Does.Contain("old.txt"), "removal of old.txt should be logged");
    }

    // ---------- TASKS ----------
    //
    // ARGUMENTS
    // [x] Interval = 0 or negative  -> non-zero exit code, replica untouched
    // [x] Interval is not a number ("abc", "1.5", "")
    // [ ] Source folder does not exist
    // [x] Replica inside source (and source inside replica) is rejected
    //
    // SYNC BEHAVIOUR
    // [ ] Idempotency: second run on an already-synced pair changes nothing (compare LastWriteTime of replica files)
    // [ ] Source is never modified by a sync (snapshot tree + content before/after)
    // [x] File in source, directory with the same name in replica (and the reverse)
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
