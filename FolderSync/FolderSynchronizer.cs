namespace FolderSync
{
    /// <summary>One synchronization pass. Knows nothing about timing or arguments.</summary>
    public sealed class FolderSynchronizer
    {
        private readonly string _source;
        private readonly string _replica;
        private readonly SyncLogger _log;

        private int _synccount = 1;

        public int SyncCount => _synccount;

        public FolderSynchronizer(string source, string replica, SyncLogger log)
        {
            _source = source;
            _replica = replica;
            _log = log;
        }

        public void SyncOnce()
        {
            try
            {
                EnsureReplicaRoot();
                CreateAndUpdateFromSource();
                RemoveExtrasFromReplica();
            }
            catch (Exception ex)
            {
                _log.Error(_synccount, $"Sync failed: {ex.Message}");
            }
            finally
            {
                _synccount++;
            }
        }

        private void EnsureReplicaRoot()
        {
            bool existed = Directory.Exists(_replica);
            Directory.CreateDirectory(_replica);
            if (!existed)
            {
                _log.Info(_synccount, "Replica directory created.");
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
                        if (File.Exists(replicaDir))
                        {
                            File.Delete(replicaDir);
                            _log.Info(
                                _synccount,
                                $"Removed file {replicaDir} to make room for directory"
                            );
                        }

                        Directory.CreateDirectory(replicaDir);
                        _log.Info(_synccount, $"Folder {d} created");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(_synccount, $"Folder {d} failed to create: {ex.Message}");
                }
            }

            var files = Directory.EnumerateFiles(_source, "*", SearchOption.AllDirectories);

            foreach (var sourceFile in files)
            {
                var relative = Path.GetRelativePath(_source, sourceFile);
                var replicaFilePath = Path.Combine(_replica, relative);

                try
                {
                    // Edgecase: If a directory exists at this path, delete it
                    if (Directory.Exists(replicaFilePath))
                    {
                        Directory.Delete(replicaFilePath, recursive: true);
                        _log.Info(
                            _synccount,
                            $"Removed directory {replicaFilePath} to make room for file"
                        );
                    }

                    bool needsCopy =
                        !File.Exists(replicaFilePath)
                        || !FileComparer.AreEqual(
                            new FileInfo(sourceFile),
                            new FileInfo(replicaFilePath)
                        );

                    // Everything is ok, no need to copy
                    if (!needsCopy)
                        continue;

                    // Check for readonly in replica
                    if (File.Exists(replicaFilePath))
                    {
                        var attributes = File.GetAttributes(replicaFilePath);

                        if ((attributes & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(
                                replicaFilePath,
                                attributes & ~FileAttributes.ReadOnly
                            );
                        }
                    }

                    // Try to copy or overwrite using temp
                    var tempPath = Path.Combine(
                        Path.GetDirectoryName(replicaFilePath)!,
                        $".{Guid.NewGuid():N}.tmp"
                    );

                    try
                    {
                        File.Copy(sourceFile, tempPath, overwrite: true);
                        File.Move(tempPath, replicaFilePath, overwrite: true);

                        _log.Info(_synccount, $"File {sourceFile} copied to {replicaFilePath}");
                    }
                    finally
                    {
                        try
                        {
                            if (File.Exists(tempPath))
                                File.Delete(tempPath);
                        }
                        catch (Exception cleanupEx)
                        {
                            _log.Error(
                                _synccount,
                                $"Could not remove temporary file {tempPath}: {cleanupEx.Message}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(
                        _synccount,
                        $"There was a problem with checking file {sourceFile} on {replicaFilePath}: {ex.Message}"
                    );
                }
            }
        }

        // Pass 2: walk REPLICA. Anything with no counterpart in source must go.
        private void RemoveExtrasFromReplica()
        {
            var files = Directory.EnumerateFiles(_replica, "*", SearchOption.AllDirectories);

            foreach (var replicaFile in files)
            {
                var relative = Path.GetRelativePath(_replica, replicaFile);
                var sourceFilePath = Path.Combine(_source, relative);

                try
                {
                    if (!File.Exists(sourceFilePath))
                    {
                        var attributes = File.GetAttributes(replicaFile);

                        if ((attributes & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(replicaFile, attributes & ~FileAttributes.ReadOnly);
                        }

                        File.Delete(replicaFile);
                        _log.Info(_synccount, $"File {replicaFile} deleted in replica");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(
                        _synccount,
                        $"There was an error deleting file {replicaFile}: {ex.Message}"
                    );
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
                        _log.Info(_synccount, $"Folder {folder} deleted");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(
                        _synccount,
                        $"There was an error deleting folder {folder}: {ex.Message}"
                    );
                }
            }
        }
    }
}
