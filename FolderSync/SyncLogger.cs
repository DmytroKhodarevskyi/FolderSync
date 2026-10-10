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

        public void Info(int syncCount, string message)
        {
            var formatted = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} N{syncCount} [INFO] {message}";
            Console.WriteLine(formatted);
            _file.WriteLine($"{formatted}");
        }

        public void Error(int syncCount, string message)
        {
            var formatted = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} N{syncCount} [ERROR] {message}";
            Console.Error.WriteLine(formatted);
            _file.WriteLine($"{formatted}");
        }

        public void Dispose() => _file.Dispose();
    }
}
