using System;
using System.Collections.Generic;
using System.Text;

namespace FolderSync
{
    /// <summary>
    /// Parse command-line arguments as: "Source" "Replica" "Interval (Seconds)" "Log"
    /// </summary>
    public static class ArgumentParser
    {
        private static bool ArePathsNested(string path1, string path2)
        {
            path1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar);
            path2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar);

            return path1.Equals(path2, StringComparison.OrdinalIgnoreCase)
                || path2.StartsWith(
                    path1 + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
                || path1.StartsWith(
                    path2 + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private static bool IsValidPath(string path, out string qualified)
        {
            qualified = "";
            try
            {
                qualified = Path.GetFullPath(path);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        // Order is fixed:
        // 0 source, 1 replica, 2 interval (seconds), 3 log file
        public static SyncOptions Parse(string[] args)
        {
            if (args is null || args?.Length != 4)
            {
                throw new ArgumentException("You must specify 4 arguments");
            }

            string srcPath = "";
            string replicaPath = "";
            string logFile = "";
            int interval = 0;

            // Check valid paths
            if (!IsValidPath(args[0], out srcPath))
            {
                throw new ArgumentException("Source path is not valid");
            }

            if (!IsValidPath(args[1], out replicaPath))
            {
                throw new ArgumentException("Replica path is not valid");
            }

            if (!IsValidPath(args[3], out logFile))
            {
                throw new ArgumentException("Log path is not valid");
            }

            if (ArePathsNested(srcPath, replicaPath))
            {
                throw new ArgumentException(
                    "Replica can't be inside source folder nor the other way around"
                );
            }

            // Check source path
            if (!Directory.Exists(srcPath))
            {
                throw new ArgumentException("Source path is not a directory");
            }

            if (Path.Exists(replicaPath) && !Directory.Exists(replicaPath))
            {
                throw new ArgumentException("Existing replica path is not a directory");
            }

            if (!int.TryParse(args[2], out interval))
            {
                throw new ArgumentException(
                    "Interval must be a number greater than 0 and less or equal than 60"
                );
            }

            // Number checks
            if (interval <= 0)
            {
                throw new ArgumentException("Interval must be more than 0");
            }

            TimeSpan tsInterval = TimeSpan.FromSeconds(interval);

            return new SyncOptions(srcPath, replicaPath, tsInterval, logFile);
        }
    }
}
