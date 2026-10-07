# FolderSync

A simple command-line folder synchronization utility that continuously monitors a source directory and keeps a replica directory in sync.

## Application Flow

1. **Parse Arguments**: The application expects exactly 4 command-line arguments in fixed order
2. **Validate Input**: Paths are validated, interval is checked, and nested paths are rejected
3. **Initialize Logger**: A log file is created to track sync operations
4. **Start Sync Loop**: Continuously synchronizes files at the specified interval
5. **Stop**: Press `Ctrl+C` to gracefully stop the application

## Command-Line Arguments

The application requires exactly **4 arguments** in the following order:

```
FolderSync.exe "Source" "Replica" "Interval" "LogFile"
```

| Argument | Description | Requirements |
|----------|-------------|--------------|
| `Source` | Path to the source directory | Must be an existing directory |
| `Replica` | Path to the replica directory | Can be created if doesn't exist; cannot be nested within Source |
| `Interval` | Sync interval in seconds | Must be a positive integer (> 0) |
| `LogFile` | Path to the log file | Directory will be created if needed |

## Example

```
FolderSync.exe "C:\MyFiles" "D:\MyFilesBackup" 30 "C:\Logs\sync.log"
```

This will:
- Sync files from `C:\MyFiles` to `D:\MyFilesBackup` every 30 seconds
- Log all operations to `C:\Logs\sync.log`

## Validation Rules

- **Paths**: Must be valid Windows paths; directories are fully qualified
- **Nesting**: Source and Replica paths cannot be nested within each other
- **Source**: Must already exist as a directory
- **Replica**: If it exists, it must be a directory
- **Interval**: Must be greater than 0 seconds

## Logging

The application logs to both **console** and **file**:

- **INFO** messages: Sync operations and status updates
- **ERROR** messages: Failures and exceptions

Log format: `yyyy-MM-dd HH:mm:ss [LEVEL] message`

## Stopping the Application

Press `Ctrl+C` to gracefully shut down the application. The program will:
- Cancel the sync loop
- Close the log file
- Return exit code 0 (success)

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success or graceful shutdown |
| `1` | Argument parsing error |

## File Comparison

Files are compared by:
1. **File size** (length must match)
2. **SHA256 hash** (content must match)

If either check fails, the file in the replica is overwritten with the source file.

## Design Notes

This is a straightforward implementation designed to avoid overengineering. Key characteristics:

- Fixed command-line argument order (no named parameters)
- Synchronous, continuous sync loop with no batching
- Simple file comparison using SHA256
- Single-pass synchronization per interval
- Graceful shutdown via Ctrl+C cancellation token
