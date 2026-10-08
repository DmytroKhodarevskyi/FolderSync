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
        public const string Usage =
            "Usage: FolderSync --source <path> --replica <path> --interval <seconds> --log <path>\n"
            + "  -s, --source    folder to copy from (must exist)\n"
            + "  -r, --replica   folder that will be made identical to source (created if missing)\n"
            + "  -i, --interval  seconds between synchronizations (positive integer)\n"
            + "  -l, --log       path of the log file\n"
            + "  -h, --help      show this help";

        private const string Source = "--source";
        private const string Replica = "--replica";
        private const string Interval = "--interval";
        private const string Log = "--log";

        private static readonly HashSet<string> KnownFlags = new()
        {
            Source,
            Replica,
            Interval,
            Log,
        };

        public static bool IsHelpRequested(string[] args) =>
            Array.Exists(args, a => a == "-h" || a == "--help");

        private static bool IsFlag(string token) =>
            Aliases.ContainsKey(token) || KnownFlags.Contains(token);

        private static readonly Dictionary<string, string> Aliases = new()
        {
            ["-s"] = Source,
            ["-r"] = Replica,
            ["-i"] = Interval,
            ["-l"] = Log,
        };

        private static Dictionary<string, string> ReadFlags(string[] args)
        {
            var result = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++)
            {
                var token = args[i];

                // Short form (-s) -> long form (--source)
                var name = Aliases.TryGetValue(token, out var canonical) ? canonical : token;

                if (!KnownFlags.Contains(name))
                    throw new ArgumentException($"Unknown option '{token}'.");

                if (result.ContainsKey(name))
                    throw new ArgumentException($"Option {name} was given more than once.");

                // The value is always the next argument; consuming it moves the index forward
                if (i + 1 >= args.Length || IsFlag(args[i + 1]))
                    throw new ArgumentException($"Option {name} needs a value.");
                var value = args[++i];

                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"Option {name} has an empty value.");

                result[name] = value;
            }

            return result;
        }

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

        private static bool IsValidPathGetQualified(string path, out string qualified)
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

        // Parse flag-based arguments: --source, --replica, --interval, --log
        public static SyncOptions Parse(string[] args)
        {
            var values = ReadFlags(args);

            foreach (var required in KnownFlags)
            {
                if (!values.ContainsKey(required))
                    throw new ArgumentException($"Missing required option {required}");
            }

            string srcPath = values[Source];
            string replicaPath = values[Replica];
            string logFile = values[Log];

            if (!int.TryParse(values[Interval], out int interval) || interval <= 0)
            {
                throw new ArgumentException("Interval must be a number greater than 0");
            }

            // Check valid paths
            if (!IsValidPathGetQualified(srcPath, out srcPath))
            {
                throw new ArgumentException("Source path is not valid");
            }

            if (!IsValidPathGetQualified(replicaPath, out replicaPath))
            {
                throw new ArgumentException("Replica path is not valid");
            }

            if (!IsValidPathGetQualified(logFile, out logFile))
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

            TimeSpan tsInterval = TimeSpan.FromSeconds(interval);

            return new SyncOptions(srcPath, replicaPath, tsInterval, logFile);
        }
    }
}
