using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Glance.Media.WinUI;

internal sealed class AudioLevelMonitor : IDisposable
{
    private const int BarCount = 5;
    private const int FftExponent = 10;
    private const int FftLength = 1 << FftExponent;
    private const int PublishIntervalMilliseconds = 32;

    private static readonly double[] BandEdges = [60, 180, 420, 950, 2400, 8000];

    private readonly object captureGate = new();
    private readonly Complex[] fftBuffer = new Complex[FftLength];
    private readonly double[] smoothedLevels = new double[BarCount];
    private readonly object spectrumGate = new();
    private WasapiLoopbackCapture? capture;
    private int fftPosition;
    private long lastPublishedTimestamp;
    private double squareSum;
    private volatile bool isRunning;

    public event EventHandler<AudioSpectrumEventArgs>? LevelsChanged;

    public bool Start()
    {
        lock (captureGate)
        {
            if (isRunning)
            {
                return true;
            }

            WasapiLoopbackCapture? newCapture = null;

            try
            {
                newCapture = new WasapiLoopbackCapture();
                newCapture.DataAvailable += HandleDataAvailable;
                newCapture.StartRecording();
                capture = newCapture;
                isRunning = true;
                return true;
            }
            catch
            {
                newCapture?.Dispose();
                capture = null;
                isRunning = false;
                return false;
            }
        }
    }

    public void Stop()
    {
        WasapiLoopbackCapture? captureToStop;

        lock (captureGate)
        {
            isRunning = false;
            captureToStop = capture;
            capture = null;
        }

        if (captureToStop is not null)
        {
            captureToStop.DataAvailable -= HandleDataAvailable;

            try
            {
                captureToStop.StopRecording();
            }
            catch
            {
            }

            captureToStop.Dispose();
        }

        lock (spectrumGate)
        {
            fftPosition = 0;
            squareSum = 0;
            Array.Clear(fftBuffer);
            Array.Clear(smoothedLevels);
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (!isRunning || sender is not WasapiLoopbackCapture activeCapture)
        {
            return;
        }

        WaveFormat format = activeCapture.WaveFormat;
        int bytesPerSample = format.BitsPerSample / 8;

        if (bytesPerSample is not (2 or 4) || format.Channels <= 0)
        {
            return;
        }

        lock (spectrumGate)
        {
            int frameSize = bytesPerSample * format.Channels;

            for (int frameOffset = 0;
                 frameOffset + frameSize <= args.BytesRecorded;
                 frameOffset += frameSize)
            {
                double sample = 0;

                for (int channel = 0; channel < format.Channels; channel++)
                {
                    int sampleOffset = frameOffset + (channel * bytesPerSample);
                    sample += bytesPerSample == 4
                        ? BitConverter.ToSingle(args.Buffer, sampleOffset) : BitConverter.ToInt16(args.Buffer, sampleOffset) / 32768d;
                }

                sample /= format.Channels;
                sample = Math.Clamp(sample, -1, 1);
                squareSum += sample * sample;
                fftBuffer[fftPosition].X = (float)(sample *
                    FastFourierTransform.HammingWindow(fftPosition, FftLength));
                fftBuffer[fftPosition].Y = 0;
                fftPosition++;

                if (fftPosition == FftLength)
                {
                    PublishSpectrum(format.SampleRate);
                    fftPosition = 0;
                    squareSum = 0;
                }
            }
        }
    }

    private void PublishSpectrum(int sampleRate)
    {
        FastFourierTransform.FFT(true, FftExponent, fftBuffer);

        double rms = Math.Sqrt(squareSum / FftLength);
        double decibels = 20 * Math.Log10(Math.Max(rms, 0.000001));
        double overallLevel = Math.Clamp((decibels + 58) / 52, 0, 1);
        double[] magnitudes = new double[BarCount];
        double maximumMagnitude = 0;

        for (int band = 0; band < BarCount; band++)
        {
            int startBin = Math.Clamp((int)Math.Ceiling(BandEdges[band] * FftLength / sampleRate), 1, (FftLength / 2) - 1);
            int endBin = Math.Clamp((int)Math.Ceiling(BandEdges[band + 1] * FftLength / sampleRate), startBin + 1, FftLength / 2);
            double magnitudeSum = 0;

            for (int bin = startBin; bin < endBin; bin++)
            {
                double real = fftBuffer[bin].X;
                double imaginary = fftBuffer[bin].Y;
                magnitudeSum += Math.Sqrt((real * real) + (imaginary * imaginary));
            }

            magnitudes[band] = magnitudeSum / Math.Max(1, endBin - startBin);
            maximumMagnitude = Math.Max(maximumMagnitude, magnitudes[band]);
        }

        for (int band = 0; band < BarCount; band++)
        {
            double relativeMagnitude = maximumMagnitude <= double.Epsilon
                ? 0
                : Math.Pow(magnitudes[band] / maximumMagnitude, 0.4);
            double target = overallLevel * (0.34 + (relativeMagnitude * 0.66));
            smoothedLevels[band] = Math.Clamp(Math.Max(target, smoothedLevels[band] * 0.74), 0, 1);
        }

        long timestamp = Stopwatch.GetTimestamp();

        if (lastPublishedTimestamp != 0 &&
            Stopwatch.GetElapsedTime(lastPublishedTimestamp, timestamp) <
                TimeSpan.FromMilliseconds(PublishIntervalMilliseconds))
        {
            return;
        }

        lastPublishedTimestamp = timestamp;
        LevelsChanged?.Invoke(this, new AudioSpectrumEventArgs([.. smoothedLevels]));
    }
}

internal sealed class AudioSpectrumEventArgs(IReadOnlyList<double> levels) : EventArgs
{
    public IReadOnlyList<double> Levels { get; } = levels;
}
