using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Glance.Clipboard.WinUI;

internal static class ClipboardDiagnostics
{
    private static readonly AsyncLocal<string?> CurrentOperation = new();
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Glance",
        "clipboard-diagnostics.log");
    private static int nextOperationId;
    private static int initialized;

    [ThreadStatic]
    private static bool isWriting;

    public static string FilePath => LogPath;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref initialized, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.FirstChanceException += HandleFirstChanceException;
        Write("Session", $"Started. Process={Environment.ProcessId}; Runtime={Environment.Version}");
    }

    public static IDisposable Begin(string operation)
    {
        Initialize();

        string? previousOperation = CurrentOperation.Value;
        string currentOperation = $"{operation}.{Interlocked.Increment(ref nextOperationId)}";
        CurrentOperation.Value = currentOperation;
        Write("Begin", currentOperation);
        return new DiagnosticScope(currentOperation, previousOperation);
    }

    public static void Write(string stage, string message)
    {
        if (isWriting)
        {
            return;
        }

        try
        {
            isWriting = true;
            string operation = CurrentOperation.Value ?? "None";
            string line = $"{DateTimeOffset.Now:O} | Thread={Environment.CurrentManagedThreadId} | " +
                $"Apartment={Thread.CurrentThread.GetApartmentState()} | Operation={operation} | " +
                $"Stage={stage} | {Normalize(message)}{Environment.NewLine}";

            Debug.Write(line);

            lock (SyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Diagnostics must never interfere with clipboard handling.
        }
        finally
        {
            isWriting = false;
        }
    }

    public static void WriteException(string stage, Exception exception) =>
        Write(stage, DescribeException(exception));

    private static void HandleFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
    {
        if (CurrentOperation.Value is not null && args.Exception is COMException exception)
        {
            WriteException("FirstChanceCOM", exception);
        }
    }

    private static string DescribeException(Exception exception)
    {
        string stackTrace = exception.StackTrace ?? "No stack trace";
        if (stackTrace.Length > 4000)
        {
            stackTrace = stackTrace[..4000];
        }

        return $"Type={exception.GetType().FullName}; HResult=0x{exception.HResult:X8}; " +
            $"Message={exception.Message}; Stack={stackTrace}";
    }

    private static string Normalize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    private sealed class DiagnosticScope(
        string operation,
        string? previousOperation) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Write("End", operation);
            CurrentOperation.Value = previousOperation;
        }
    }
}
