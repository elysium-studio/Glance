using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Glance.VoiceNotes.WinUI;

internal sealed class WindowsVoiceRecordingService :
    IVoiceRecordingService
{
    private const int LevelCount = 12;

    private readonly object gate = new();
    private readonly string recordingsPath;
    private WaveInEvent? capture;
    private string? currentFilePath;
    private long recordingStartedTimestamp;
    private WaveFileWriter? writer;

    public WindowsVoiceRecordingService(string recordingsPath)
    {
        this.recordingsPath = recordingsPath;
        Directory.CreateDirectory(recordingsPath);
    }

    public event EventHandler<VoiceLevelsChangedEventArgs>? LevelsChanged;

    public event EventHandler<VoiceRecordingCompletedEventArgs>? RecordingCompleted;

    public bool IsRecording { get; private set; }

    public IReadOnlyList<VoiceNote> GetRecentRecordings(int maximumCount)
    {
        if (maximumCount <= 0)
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(recordingsPath, "*.wav").Select(CreateVoiceNote).OfType<VoiceNote>().OrderByDescending(recording => recording.CreatedAt)
                .Take(maximumCount).ToArray();
        }
        catch
        {
            return [];
        }
    }

    public bool StartRecording()
    {
        lock (gate)
        {
            if (IsRecording)
            {
                return true;
            }

            WaveInEvent? newCapture = null;
            WaveFileWriter? newWriter = null;
            string filePath = Path.Combine(recordingsPath, $"Voice note {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");

            try
            {
                newCapture = new WaveInEvent
                {
                    BufferMilliseconds = 40,
                    NumberOfBuffers = 3,
                    WaveFormat = new WaveFormat(44100, 16, 1)
                };
                newWriter = new WaveFileWriter(filePath, newCapture.WaveFormat);
                newCapture.DataAvailable += HandleDataAvailable;
                newCapture.RecordingStopped += HandleRecordingStopped;

                capture = newCapture;
                writer = newWriter;
                currentFilePath = filePath;
                recordingStartedTimestamp = Stopwatch.GetTimestamp();
                IsRecording = true;
                newCapture.StartRecording();
                return true;
            }
            catch
            {
                IsRecording = false;
                capture = null;
                writer = null;
                currentFilePath = null;
                newCapture?.Dispose();
                newWriter?.Dispose();
                TryDeleteFile(filePath);
                return false;
            }
        }
    }

    public void StopRecording()
    {
        WaveInEvent? captureToStop;

        lock (gate)
        {
            if (!IsRecording)
            {
                return;
            }

            IsRecording = false;
            captureToStop = capture;
        }

        try
        {
            captureToStop?.StopRecording();
        }
        catch (Exception exception)
        {
            CompleteRecording(exception);
        }
    }

    public bool TryOpen(VoiceNote recording)
    {
        try
        {
            if (!File.Exists(recording.FilePath))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(recording.FilePath)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryDelete(VoiceNote recording)
    {
        try
        {
            if (File.Exists(recording.FilePath))
            {
                File.Delete(recording.FilePath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        StopRecording();
        GC.SuppressFinalize(this);
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs args)
    {
        WaveFileWriter? activeWriter;

        lock (gate)
        {
            activeWriter = writer;
        }

        if (activeWriter is null)
        {
            return;
        }

        try
        {
            activeWriter.Write(args.Buffer, 0, args.BytesRecorded);
            activeWriter.Flush();
            LevelsChanged?.Invoke(this, new VoiceLevelsChangedEventArgs(CalculateLevels(args.Buffer, args.BytesRecorded)));
        }
        catch (Exception)
        {
            StopRecording();
        }
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs args) =>
        CompleteRecording(args.Exception);

    private void CompleteRecording(Exception? error)
    {
        WaveInEvent? completedCapture;
        WaveFileWriter? completedWriter;
        string? completedFilePath;
        TimeSpan duration;

        lock (gate)
        {
            if (capture is null &&
                writer is null &&
                currentFilePath is null &&
                recordingStartedTimestamp == 0)
            {
                return;
            }

            completedCapture = capture;
            completedWriter = writer;
            completedFilePath = currentFilePath;
            duration = recordingStartedTimestamp == 0
                ? TimeSpan.Zero
                : Stopwatch.GetElapsedTime(recordingStartedTimestamp);

            capture = null;
            writer = null;
            currentFilePath = null;
            recordingStartedTimestamp = 0;
            IsRecording = false;
        }

        if (completedCapture is not null)
        {
            completedCapture.DataAvailable -= HandleDataAvailable;
            completedCapture.RecordingStopped -= HandleRecordingStopped;
            completedCapture.Dispose();
        }

        completedWriter?.Dispose();

        VoiceNote? recording = null;

        if (error is null &&
            completedFilePath is not null &&
            File.Exists(completedFilePath) &&
            new FileInfo(completedFilePath).Length > 44)
        {
            recording = new VoiceNote(completedFilePath, File.GetCreationTime(completedFilePath), duration);
        }
        else if (completedFilePath is not null)
        {
            TryDeleteFile(completedFilePath);
        }

        RecordingCompleted?.Invoke(this, new VoiceRecordingCompletedEventArgs(recording, error));
    }

    private static IReadOnlyList<double> CalculateLevels(
        byte[] buffer,
        int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 2;

        if (sampleCount == 0)
        {
            return new double[LevelCount];
        }

        double[] levels = new double[LevelCount];

        for (int levelIndex = 0; levelIndex < LevelCount; levelIndex++)
        {
            int startSample = levelIndex * sampleCount / LevelCount;
            int endSample = Math.Max(startSample + 1, (levelIndex + 1) * sampleCount / LevelCount);
            double peak = 0;
            double squareSum = 0;
            int samplesInLevel = 0;

            for (int sampleIndex = startSample;
                 sampleIndex < endSample && sampleIndex < sampleCount;
                 sampleIndex++)
            {
                short sample = BitConverter.ToInt16(buffer, sampleIndex * 2);
                double normalizedSample = sample / 32768d;
                peak = Math.Max(peak, Math.Abs(normalizedSample));
                squareSum += normalizedSample * normalizedSample;
                samplesInLevel++;
            }

            double rms = samplesInLevel == 0
                ? 0
                : Math.Sqrt(squareSum / samplesInLevel);
            double rmsLevel = ToDisplayLevel(rms);
            double peakLevel = ToDisplayLevel(peak);

            levels[levelIndex] = Math.Clamp((rmsLevel * 0.72) + (peakLevel * 0.28), 0, 1);
        }

        return levels;
    }

    private static double ToDisplayLevel(double amplitude)
    {
        double decibels = 20 * Math.Log10(Math.Max(amplitude, 0.000001));

        return Math.Clamp((decibels + 52) / 40, 0, 1);
    }

    private static VoiceNote? CreateVoiceNote(string filePath)
    {
        try
        {
            using WaveFileReader reader = new(filePath);

            return new VoiceNote(filePath, File.GetCreationTime(filePath), reader.TotalTime);
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }
}
