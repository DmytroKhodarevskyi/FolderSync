using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FolderSync
{
    /// <summary>
    /// Checks two files in content equality
    /// </summary>
    public static class FileComparer
    {
        public static bool AreEqual(FileInfo source, FileInfo replica)
        {
            if (!replica.Exists || !source.Exists)
                return false;

            if (source.Length != replica.Length)
                return false;

            return string.Equals(
                ComputeHash(source.FullName),
                ComputeHash(replica.FullName),
                StringComparison.Ordinal
            );
        }

        private static string ComputeHash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
