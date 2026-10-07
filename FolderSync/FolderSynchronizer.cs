using System;
using System.Collections.Generic;
using System.Text;

namespace FolderSync
{
    /// <summary>One synchronization pass. Knows nothing about timing or arguments.</summary>
    public sealed class FolderSynchronizer
    {
        private readonly string _source;
        private readonly string _replica;
        private readonly SyncLogger _log;

        public FolderSynchronizer(string source, string replica, SyncLogger log)
        {
            _source = source;
            _replica = replica;
            _log = log;
        }

        public void SyncOnce()
        {
            EnsureReplicaRoot();
            CreateAndUpdateFromSource();
            RemoveExtrasFromReplica();
        }

        private void EnsureReplicaRoot()
        {
            bool existed = Directory.Exists(_replica);
            Directory.CreateDirectory(_replica);
            if (!existed)
            {
                _log.Info("Replica directory created.");
            }
        }

        // Pass 1: walk SOURCE. Everything in source must exist and match in replica.
        private void CreateAndUpdateFromSource()
        {
            var directories = Directory.EnumerateDirectories(
                _source,
                "*",
                SearchOption.AllDirectories
            );

            foreach (var d in directories)
            {
                var relative = Path.GetRelativePath(_source, d);

                var replicaDir = Path.Combine(_replica, relative);

                try
                {
                    if (!Directory.Exists(replicaDir))
                    {
                        Directory.CreateDirectory(replicaDir);
                        _log.Info($"Folder {d} created");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"Folder {d} failed to create: {ex.Message}");
                }
            }

            var files = Directory.EnumerateFiles(_source, "*", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                var relative = Path.GetRelativePath(_source, f);
                var replicaFilePath = Path.Combine(_replica, relative);

                try
                {
                    // Copy if don't exist
                    if (!File.Exists(replicaFilePath))
                    {
                        File.Copy(f, replicaFilePath);
                        _log.Info($"File {f} copied to {replicaFilePath}");
                    }
                    // Overwrite corrupted file
                    else if (!FileComparer.AreEqual(new FileInfo(f), new FileInfo(replicaFilePath)))
                    {
                        File.Copy(f, replicaFilePath, overwrite: true);
                        _log.Info($"File {f} overwritten on {replicaFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(
                        $"There was a problem with checking file {f} on {replicaFilePath}: {ex.Message}"
                    );
                }
            }
        }

        // Pass 2: walk REPLICA. Anything with no counterpart in source must go.
        private void RemoveExtrasFromReplica()
        {
            // Edge case: same name is a file in source but a directory in replica (or the reverse)

            var files = Directory.EnumerateFiles(_replica, "*", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                var relative = Path.GetRelativePath(_replica, f);
                var sourceFilePath = Path.Combine(_source, relative);

                try
                {
                    if (!File.Exists(sourceFilePath))
                    {
                        File.Delete(f);
                        _log.Info($"File {f} deleted in replica");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"There was an error deleting file {f}: {ex.Message}");
                }
            }

            // Going from depth, so they are empty when deleted
            var folders = Directory
                .EnumerateDirectories(_replica, "*", SearchOption.AllDirectories)
                .OrderByDescending(x => x.Length);

            foreach (var folder in folders)
            {
                var relative = Path.GetRelativePath(_replica, folder);
                var sourceRelativePath = Path.Combine(_source, relative);

                try
                {
                    if (!Directory.Exists(sourceRelativePath))
                    {
                        Directory.Delete(folder);
                        _log.Info($"Folder {folder} deleted");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"There was an error deleting folder {folder}: {ex.Message}");
                }
            }
        }
    }
}
