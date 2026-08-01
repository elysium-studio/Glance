using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class WakeWordTestWindow :
    Window
{
    private const int WindowWidth = 720;
    private const int WindowHeight = 620;
    private static readonly TimeSpan ReadinessProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadinessPreparationTimeout = TimeSpan.FromMinutes(2);
    private static readonly Regex WakePhraseExpression = new(@"\b(?:hey[\s,.:;!?-]+)?glance\b[\s,.:;!?-]*(?<command>.*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static WakeWordTestWindow? current;

    private SpeechRecognitionModel? speechModel;
    private StreamingRecognition? streamingRecognition;
    private WakeWordAudioCapture? audioCapture;
    private bool initialized;

    private WakeWordTestWindow()
    {
        InitializeComponent();
        Title = "Glance wake-word test";
        Closed += HandleClosed;
        Activated += HandleActivated;

        OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsMaximizable = false;
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
    }

    public ObservableCollection<string> DetectedCommands { get; } = [];

    public static void Open()
    {
        if (current is not null)
        {
            current.Activate();
            return;
        }

        current = new WakeWordTestWindow();
        current.DetectedCommandsList.ItemsSource = current.DetectedCommands;
        current.Activate();
    }

    private async void HandleActivated(object sender, WindowActivatedEventArgs args)
    {
        if (initialized || args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        initialized = true;
        await PrepareAndStartAsync();
    }

    private async void HandleListeningClick(object sender, RoutedEventArgs args)
    {
        ListeningButton.IsEnabled = false;

        if (streamingRecognition is null)
        {
            await StartRecognitionAsync();
        }
        else
        {
            await StopRecognitionAsync();
            SetStatus("Listening stopped. Recognition remains completely local.", false);
            DispatcherQueue.TryEnqueue(() =>
            {
                ListeningButton.Content = "Start listening";
                ListeningButton.IsEnabled = true;
            });
        }
    }

    private async void HandleClosed(object sender, WindowEventArgs args)
    {
        Activated -= HandleActivated;
        Closed -= HandleClosed;
        await StopRecognitionAsync();
        current = null;
    }

    private async Task PrepareAndStartAsync()
    {
        if (!HasPackageIdentity())
        {
            SetStatus("Package identity is missing. Launch this build through Register-WakeWordTest.ps1.", false);
            return;
        }

        try
        {
            AIFeatureReadyState readyState = await RunOnStaAsync(() => SpeechRecognitionModel.GetReadyState()).WaitAsync(ReadinessProbeTimeout);

            if (readyState == AIFeatureReadyState.NotReady)
            {
                SetStatus("Downloading or preparing the Microsoft on-device speech model…", false);
                await RunOnStaAsync(async () => await SpeechRecognitionModel.EnsureReadyAsync()).WaitAsync(ReadinessPreparationTimeout);
                readyState = await RunOnStaAsync(() => SpeechRecognitionModel.GetReadyState()).WaitAsync(ReadinessProbeTimeout);
            }

            if (readyState != AIFeatureReadyState.Ready)
            {
                SetStatus($"Windows reported the speech model state as {readyState}.", false);
                return;
            }

            await StartRecognitionAsync();
        }
        catch (TimeoutException)
        {
            SetStatus("Windows did not respond while preparing the speech model. The test window remains responsive.", false);
        }
        catch (Exception exception)
        {
            SetStatus($"Speech preparation failed: {exception.Message}", false);
        }
    }

    private async Task StartRecognitionAsync()
    {
        try
        {
            await StopRecognitionAsync();

            SpeechRecognitionModelResult modelResult = await SpeechRecognitionModel.TryCreateAsync();
            speechModel = modelResult.SpeechModel;

            if (speechModel is null)
            {
                SetStatus("Windows could not create the local speech model.", false);
                return;
            }

            audioCapture = new WakeWordAudioCapture();
            streamingRecognition = new StreamingRecognition(audioCapture.CreateAudioConfiguration(), speechModel);
            streamingRecognition.Recognizing += HandleRecognizing;
            streamingRecognition.Recognized += HandleRecognized;
            audioCapture.Start();
            await streamingRecognition.StartContinuousRecognitionAsync();

            DispatcherQueue.TryEnqueue(() =>
            {
                ListeningButton.Content = "Stop listening";
                ListeningButton.IsEnabled = true;
            });
            SetStatus("Listening locally for “Glance” or “Hey Glance”.", true);
        }
        catch (Exception exception)
        {
            await StopRecognitionAsync();
            SetStatus($"Recognition failed: {exception.Message}", false);
        }
    }

    private async Task StopRecognitionAsync()
    {
        StreamingRecognition? recognition = streamingRecognition;
        streamingRecognition = null;

        if (recognition is not null)
        {
            recognition.Recognizing -= HandleRecognizing;
            recognition.Recognized -= HandleRecognized;

            try
            {
                recognition.StopContinuousRecognition();
            }
            catch (Exception)
            {
            }

            recognition.Dispose();
        }

        speechModel?.Dispose();
        speechModel = null;

        if (audioCapture is not null)
        {
            await audioCapture.DisposeAsync();
            audioCapture = null;
        }
    }

    private void HandleRecognizing(StreamingRecognition sender, StreamingRecognizingEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => TranscriptionText.Text = args.Text);

    private void HandleRecognized(StreamingRecognition sender, StreamingRecognizedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => ProcessRecognizedText(args.Text));

    private void ProcessRecognizedText(string text)
    {
        TranscriptionText.Text = string.IsNullOrWhiteSpace(text)
            ? "Listening…"
            : text;

        Match match = WakePhraseExpression.Match(text);

        if (!match.Success)
        {
            return;
        }

        string command = match.Groups["command"].Value.Trim();
        DetectedCommands.Insert(0, string.IsNullOrWhiteSpace(command)
            ? "Wake phrase detected"
            : command);
        SetStatus("Wake phrase detected.", true);
    }

    private void SetStatus(string text, bool active)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => SetStatus(text, active));
            return;
        }

        StatusText.Text = text;
        StatusIcon.Glyph = active ? "\uE8FB" : "\uE720";
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Package.Current.Id.Name);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static Task<T> RunOnStaAsync<T>(Func<T> operation)
    {
        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                completion.SetResult(operation());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Glance speech model probe"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static Task RunOnStaAsync(Func<Task> operation)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                operation().GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Glance speech model preparation"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class WakeWordAudioCapture :
        IAsyncDisposable
    {
        private const int TargetSampleRate = 16000;
        private const int BufferDurationMilliseconds = 40;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly SpeechAudioProvider provider = new();
        private WasapiCapture? capture;
        private Task? pumpTask;

        public AudioConfiguration CreateAudioConfiguration() =>
            AudioConfiguration.ForProvider(provider);

        public void Start()
        {
            capture = new WasapiCapture();
            BufferedWaveProvider buffer = new(capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            capture.DataAvailable += (_, args) => buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);

            ISampleProvider sampleProvider = buffer.ToSampleProvider();
            sampleProvider = sampleProvider.WaveFormat.Channels == 1
                ? sampleProvider
                : new DownmixSampleProvider(sampleProvider);
            sampleProvider = sampleProvider.WaveFormat.SampleRate == TargetSampleRate
                ? sampleProvider
                : new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate);

            IWaveProvider waveProvider = new SampleToWaveProvider16(sampleProvider);
            capture.StartRecording();
            pumpTask = Task.Run(() => PumpAsync(waveProvider, cancellationTokenSource.Token));
        }

        public async ValueTask DisposeAsync()
        {
            await cancellationTokenSource.CancelAsync();

            if (capture is not null)
            {
                try
                {
                    capture.StopRecording();
                }
                catch (Exception)
                {
                }
            }

            if (pumpTask is not null)
            {
                try
                {
                    await pumpTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            capture?.Dispose();
            cancellationTokenSource.Dispose();
        }

        private async Task PumpAsync(IWaveProvider waveProvider, CancellationToken cancellationToken)
        {
            int bytesPerBuffer = TargetSampleRate * sizeof(short) * BufferDurationMilliseconds / 1000;
            byte[] buffer = new byte[bytesPerBuffer];
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(BufferDurationMilliseconds));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    provider.PushData(bytesRead == buffer.Length ? buffer : buffer[..bytesRead]);
                }
            }
        }

        private sealed class DownmixSampleProvider(ISampleProvider source) :
            ISampleProvider
        {
            private readonly ISampleProvider source = source;
            private readonly float[] sourceBuffer = new float[4096];

            public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

            public int Read(float[] buffer, int offset, int count)
            {
                int sourceChannels = source.WaveFormat.Channels;
                int sourceSamplesRequired = Math.Min(sourceBuffer.Length, count * sourceChannels);
                int sourceSamplesRead = source.Read(sourceBuffer, 0, sourceSamplesRequired);
                int framesRead = sourceSamplesRead / sourceChannels;

                for (int frame = 0; frame < framesRead; frame++)
                {
                    float sum = 0;

                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        sum += sourceBuffer[(frame * sourceChannels) + channel];
                    }

                    buffer[offset + frame] = sum / sourceChannels;
                }

                return framesRead;
            }
        }
    }
}
