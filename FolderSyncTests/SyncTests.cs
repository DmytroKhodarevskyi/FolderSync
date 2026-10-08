using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace FolderSync.Tests;

/// <summary>
/// Black-box tests: every test runs the program through Program.MainImpl(args, cancellationToken),
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

    private void AssertTreesEqual(string treeString1, string treeString2)
    {
        var tree1 = Tree(treeString1);
        var tree2 = Tree(treeString2);

        Assert.That(tree2, Is.EqualTo(tree1), "Folder structure differs");

        foreach (var entry in tree1.Where(e => !e.IsDirectory))
        {
            var tree1Path = Path.Combine(treeString1, entry.Path);
            var tree2Path = Path.Combine(treeString2, entry.Path);

            Assert.That(
                File.ReadAllBytes(tree2Path),
                Is.EqualTo(File.ReadAllBytes(tree1Path)),
                $"Content differs: {entry.Path}"
            );
        }
    }

    [Test]
    public void ArgumentParser_HelpCalled_ReturnsZero()
    {
        string[] arguments = new[] { "--help" };

        var exitCode = Run(arguments);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void ArgumentParser_SourceDoesNotExist_ReturnsNonZero()
    {
        string[] arguments = new[]
        {
            SourceFlag,
            "random",
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
    public void ArgumentParser_IntervalIsNotANumber_ReturnsNonZero()
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
    public void ArgumentParser_FlagDuplicates_ReturnsNonZero()
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
    public void ArgumentParser_FlagIdentifierDuplicates_ReturnsNonZero()
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
    public void ArgumentParser_NestedSourceAndReplica_ReturnsNonZero()
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
    public void ArgumentParser_MissingArgument_ReturnsNonZero()
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
    public void MainImpl_NoChangeInSource_DoesNotChangeContents()
    {
        Write(_source, "a.txt", "hello");

        Assert.That(Run(), Is.EqualTo(0));
        Assert.That(Directory.Exists(_replica));

        var replicaPath = Path.Combine(_replica, "a.txt");

        Assert.That(File.Exists(replicaPath), Is.True);
        var last = File.GetLastWriteTime(replicaPath);

        Assert.That(Run(), Is.EqualTo(0));

        var newlast = File.GetLastWriteTime(replicaPath);
        Assert.That(last == newlast, Is.True);
    }

    [Test]
    public void MainImpl_ReplicaModified_SourceIsNotModified()
    {
        Write(_source, "a.txt", "hello");

        var firstSource = Tree(_source);

        Assert.That(Run(), Is.EqualTo(0));

        var newSource = Tree(_source);

        Assert.That(firstSource, Is.EqualTo(newSource), "Folder structure differs");
    }

    [Test]
    public void MainImpl_BigFileCreatedInSource_Synchronized()
    {
        Write(_source, "big.txt", new string('A', 50 * 1024 * 1024));

        Assert.That(Run(), Is.EqualTo(0));
        AssertTreesEqual(_source, _replica);
    }

    [Test]
    public void MainImpl_ReplicaDoesNotExist_CreatesReplicaFolder()
    {
        Write(_source, "a.txt", "hello");

        Assert.That(Run(), Is.EqualTo(0));

        Assert.That(Directory.Exists(_replica));
    }

    [Test]
    public void MainImpl_ReplicaMatches_CopiesNestedFilesAndFolders()
    {
        Write(_source, "a.txt", "a");
        Write(_source, Path.Combine("dir1", "b.txt"), "b");
        Write(_source, Path.Combine("dir1", "dir2", "c.txt"), "c");

        Run();

        AssertTreesEqual(_source, _replica);
    }

    [Test]
    public void MainImpl_SameNameFileSourceAndDirectoryReplica_NoDataLoss()
    {
        // Replica has directory "a"
        Directory.CreateDirectory(Path.Combine(_replica, "a"));

        // Source has file "a" (not directory)
        Write(_source, "a", "content_of_file");

        Run(timeoutSeconds: timeout * 2);

        // After sync: replica should also have file "a" (directory replaced by file)
        AssertTreesEqual(_source, _replica);
        Assert.That(
            Directory.Exists(Path.Combine(_replica, "a")),
            Is.False,
            "Directory should be removed"
        );
        Assert.That(File.Exists(Path.Combine(_replica, "a")), Is.True, "File should exist");

        // Prefferably no output in stderr
    }

    [Test]
    public void MainImpl_SameNameFileReplicaAndDirectorySource_NoDataLoss()
    {
        // Source has directory "a"
        Directory.CreateDirectory(Path.Combine(_source, "a"));

        // Replica has file "a" (not directory)
        Write(_replica, "a", "content_of_file");

        Run(timeoutSeconds: timeout * 2);

        // After sync: replica should also have directory "a" (file replaced by directory)
        AssertTreesEqual(_source, _replica);
        Assert.That(
            Directory.Exists(Path.Combine(_replica, "a")),
            Is.True,
            "Directory should be created"
        );
        Assert.That(File.Exists(Path.Combine(_replica, "a")), Is.False, "File should be removed");

        // Prefferably no output in stderr
    }

    [Test]
    public void MainImpl_CreatesEmptyDirectories_ReplicatesEmptyFolders()
    {
        Directory.CreateDirectory(Path.Combine(_source, "empty", "nested"));

        Run();

        Assert.That(Directory.Exists(Path.Combine(_replica, "empty", "nested")));
    }

    [Test]
    public void MainImpl_ContentDiffersEvenIfSizeMatches_OverwritesFile()
    {
        Write(_source, "a.txt", "AAAA");
        Write(_replica, "a.txt", "BBBB"); // same length, different content

        Run();

        Assert.That(File.ReadAllText(Path.Combine(_replica, "a.txt")), Is.EqualTo("AAAA"));
    }

    [Test]
    public void MainImpl_NotInSource_RemovesExtraFilesAndFolders()
    {
        Write(_source, "keep.txt", "keep");
        Write(_replica, "keep.txt", "keep");
        Write(_replica, "extra.txt", "x");
        Write(_replica, Path.Combine("extra_dir", "deep", "y.txt"), "y");

        Run();

        AssertTreesEqual(_source, _replica);

        Assert.That(File.Exists(Path.Combine(_replica, "extra.txt")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_replica, "extra_dir")), Is.False);
    }

    [Test]
    public void MainImpl_DoesNotStopOtherFilesFromSyncing_LockedFile()
    {
        Write(_source, "locked.txt", "locked content");
        Write(_source, "normal.txt", "normal content");

        var lockedFile = Path.Combine(_source, "locked.txt");

        using var lockHandle = new FileStream(
            lockedFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        );

        // Run your sync while lockedFile is exclusively opened.
        Run();

        // The locked file should NOT have been copied.
        Assert.That(File.Exists(Path.Combine(_replica, "locked.txt")), Is.False);

        // But the other file should have been copied.
        Assert.That(File.Exists(Path.Combine(_replica, "normal.txt")), Is.True);

        Assert.That(
            File.ReadAllText(Path.Combine(_replica, "normal.txt")),
            Is.EqualTo("normal content")
        );
    }

    [Test]
    public void MainImpl_ReadonlyFileDifferentContent_IsOverwritten()
    {
        Write(_source, "file.txt", "new content");
        Write(_replica, "file.txt", "old content");

        var sourceFile = Path.Combine(_source, "file.txt");
        var replicaFile = Path.Combine(_replica, "file.txt");

        File.SetAttributes(replicaFile, File.GetAttributes(replicaFile) | FileAttributes.ReadOnly);

        Run();

        Assert.That(File.ReadAllText(replicaFile), Is.EqualTo("new content"));
    }

    [Test]
    public void MainImpl_ReadonlyFileDoesNotExistInSource_IsRemoved()
    {
        Write(_replica, "file.txt", "some content");

        var replicaFile = Path.Combine(_replica, "file.txt");

        File.SetAttributes(replicaFile, File.GetAttributes(replicaFile) | FileAttributes.ReadOnly);

        Run();

        Assert.That(File.Exists(replicaFile), Is.EqualTo(false));
    }

    [Test]
    public void MainImpl_FileWithDots_IsSynchronized()
    {
        Write(_source, ".file.txt", "some content");

        Run();

        AssertTreesEqual(_source, _replica);
    }

    [Test]
    public void MainImpl_FileWithUnicode_IsSynchronized()
    {
        Write(_source, "Привет Вим! 😀.txt", "Привет Вим! 😀");

        Run();

        AssertTreesEqual(_source, _replica);
    }

    [Test]
    public void MainImpl_FileWithZeroBytes_IsSynchronized()
    {
        var path = Path.Combine(_source, "empty.txt");

        File.WriteAllBytes(path, Array.Empty<byte>());

        Run();

        AssertTreesEqual(_source, _replica);
    }

    [Test]
    public void SyncLogger_ContainsOperations_WritesLogFile()
    {
        Write(_source, "new.txt", "n");
        Write(_replica, "old.txt", "o");

        Run();

        var log = File.ReadAllText(_log);
        Assert.That(log, Does.Contain("new.txt"), "copy of new.txt should be logged");
        Assert.That(log, Does.Contain("old.txt"), "removal of old.txt should be logged");
    }

    [Test]
    public void SyncLogger_IdenticalFoldersAlready_NothingIsLogged()
    {
        Run();

        var log = File.ReadAllText(_log);

        int numLines = log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(
            numLines,
            Is.EqualTo(1),
            "Only \"replica directory created\" log should be logged"
        );

        Run();

        log = File.ReadAllText(_log);
        int numLines2 = log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(numLines, Is.EqualTo(numLines2));
    }

    [Test]
    public void SyncLogger_ApplicationSyncs_LogAndOutputAreTheSame()
    {
        var stdout = new StringWriter();
        Console.SetOut(stdout);

        Run();

        var log = File.ReadAllText(_log);

        Assert.That(stdout.ToString(), Is.EqualTo(log));
    }

    [Test]
    public void SyncLogger_ItDidNotExistAndAppRuns_LogFileCreates()
    {
        Assert.That(
            File.Exists(_log),
            Is.False,
            "Log should not be created by the app if it never ran before"
        );

        Run();

        Assert.That(File.Exists(_log), Is.True, "Log should be created after app runs");
    }

    [Test]
    public void SyncLogger_NewLogCreated_LogAppends()
    {
        Write(_source, "test.txt", "test");

        Run();

        var log1 = File.ReadAllText(_log);

        Write(_source, "test2.txt", "test");

        Run();

        var log2 = File.ReadAllText(_log);

        Assert.That(log2.Contains(log1), Is.True, "Log should append new entries, now overwrite");
    }

    // ---------- TASKS ----------
    //
    // ARGUMENTS
    // [x] Interval = 0 or negative  -> non-zero exit code, replica untouched
    // [x] Interval is not a number ("abc", "1.5", "")
    // [x] Source folder does not exist
    // [x] Replica inside source (and source inside replica) is rejected
    //
    // SYNC BEHAVIOUR
    // [x] Idempotency: second run on an already-synced pair changes nothing (compare LastWriteTime of replica files)
    // [x] Source is never modified by a sync (snapshot tree + content before/after)
    // [x] File in source, directory with the same name in replica (and the reverse)
    // [x] Names with spaces, unicode and dots; file without extension; zero-byte file
    // [x] Large file (e.g. 50 MB) syncs correctly and in reasonable time
    // [x] Read-only file in replica gets overwritten or removed
    // [x] One locked/unreadable file does not stop the other files from syncing (open it with FileShare.None)
    // [ ] Handle long paths (files or directories)
    // [ ] Handle symbolic links
    //
    // LOGGING
    // [x] Log is appended, not overwritten, across two program runs
    // [x] Log directory that does not exist yet gets created
    // [x] Console output contains the same operations as the log (redirect with Console.SetOut)
    // [x] Nothing is logged for files that are already identical
}
