# FolderSync

A simple command-line folder synchronization utility that continuously monitors a source directory and keeps a replica directory in sync.

## Application Flow

1. **Parse Arguments**: The application expects exactly 4 command-line arguments with flags
2. **Validate Input**: Paths are validated, interval is checked, and nested paths are rejected
3. **Initialize Logger**: A log file is created to track sync operations
4. **Start Sync Loop**: Continuously synchronizes files at the specified interval
5. **Stop**: Press `Ctrl+C` to gracefully stop the application

## Command-Line Arguments

The application requires exactly **4 arguments** which are parsed using flags:

```
FolderSync.exe "Source" "Replica" "Interval" "LogFile"
```

| Argument | Flag | Description | Requirements |
|----------|-------------|-------------|--------------|
| `Source` | --source / -s | Path to the source directory | Must be an existing directory |
| `Replica` | --replica / -r | Path to the replica directory | Can be created if doesn't exist; cannot be nested within Source |
| `Interval` | --interval / -i | Sync interval in seconds | Must be a positive integer (> 0) |
| `LogFile` | --log / -l | Path to the log file | Directory will be created if needed |
| `Help` | --help / -h | Prints usage | - |

## Example

```
FolderSync.exe --source "C:\MyFiles" --replica "D:\MyFilesBackup" --interval 30 --log "C:\Logs\sync.log"
```

This will:
- Sync files from `C:\MyFiles` to `D:\MyFilesBackup` every 30 seconds
- Log all operations to `C:\Logs\sync.log`

Arguments can be passed in any order

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

Log format: `yyyy-MM-dd HH:mm:ss Nn [LEVEL] message` where **n** is the number of sync

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

Key characteristics:

- Command-line argument parsing with flags
- Synchronous, continuous sync loop with no batching
- Simple file comparison using SHA256
- Single-pass synchronization per interval
- Graceful shutdown via Ctrl+C cancellation token
- Symbolic links are not supported to avoid dev mode
- Log must not be in source or replica to avoid conflicts
- Program is designed for Windows system

## Tests

This repository also contains tests, which are for general application verification, 
i did not create test cases for *everything*.
