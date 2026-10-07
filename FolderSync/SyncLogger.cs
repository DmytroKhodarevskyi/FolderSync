using System;
using System.Collections.Generic;
using System.Text;

namespace FolderSync
{
    public sealed class SyncLogger : IDisposable
    {
        private readonly StreamWriter _file;

        public SyncLogger(string logPath)
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _file = new StreamWriter(logPath, append: true) { AutoFlush = true };
        }

        public void Info(string message)
        {
            var formatted = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}";
            Console.WriteLine(formatted);
            _file.Write($"{formatted}\n");
        }

        public void Error(string message)
        {
            var formatted = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}";
            Console.Error.WriteLine(formatted);
            _file.Write($"{formatted}\n");
        }

        public void Dispose() => _file.Dispose();
    }
}
