using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Speech;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Windows.ApplicationModel;

namespace Glance.SpeechToText.WinUI;

public sealed partial class WindowsSpeechRecognitionService :
    ISpeechRecognitionService
{
    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(5);
    private readonly Lock availabilityLock = new();
    private Task<SpeechRecognitionAvailability>? availabilityTask;
    private SpeechRecognitionModel? speechModel;
    private StreamingRecognition? streamingRecognition;
    private LiveSpeechAudioCapture? audioCapture;

    public SpeechRecognitionAvailability Availability { get; private set; } = SpeechRecognitionAvailability.Checking;

    public bool IsListening { get; private set; }

    public event EventHandler<SpeechRecognitionAvailabilityChangedEventArgs>? AvailabilityChanged;

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;

    public event EventHandler? ListeningStopped;

    public async Task CheckAvailabilityAsync() => SetAvailability(await GetAvailabilityAsync());

    public async Task<bool> EnsureModelAsync()
    {
        if (!HasPackageIdentity())
        {
            SetAvailability(SpeechRecognitionAvailability.PackageIdentityRequired);
            return false;
        }

        if (!IsRuntimeCompatible())
        {
            SetAvailability(SpeechRecognitionAvailability.Unsupported);
            return false;
        }

        try
        {
            _ = await SpeechRecognitionModel.EnsureReadyAsync();
            SetAvailability(await GetAvailabilityAsync());
            return Availability == SpeechRecognitionAvailability.Ready;
        }
        catch (Exception)
        {
            SetAvailability(await GetAvailabilityAsync());
            return false;
        }
    }

    public async Task<bool> StartAsync(SpeechAudioSource audioSource)
    {
        if (IsListening)
        {
            return true;
        }

        if (!IsRuntimeCompatible())
        {
            SetAvailability(SpeechRecognitionAvailability.Unsupported);
            return false;
        }

        if (Availability != SpeechRecognitionAvailability.Ready)
        {
            await CheckAvailabilityAsync();
        }

        if (Availability != SpeechRecognitionAvailability.Ready)
        {
            return false;
        }

        try
        {
            await ReleaseRecognitionAsync();

            SpeechRecognitionModelResult modelResult = await SpeechRecognitionModel.TryCreateAsync();
            speechModel = modelResult.SpeechModel;

            if (speechModel is null)
            {
                SetAvailability(SpeechRecognitionAvailability.Unavailable);
                return false;
            }

            audioCapture = new LiveSpeechAudioCapture();
            AudioConfiguration audioConfiguration = audioCapture.CreateAudioConfiguration();
            streamingRecognition = new StreamingRecognition(audioConfiguration, speechModel);
            streamingRecognition.Recognizing += HandleRecognizing;
            streamingRecognition.Recognized += HandleRecognized;
            audioCapture.Start(audioSource);
            await streamingRecognition.StartContinuousRecognitionAsync();
            IsListening = true;
            return true;
        }
        catch (Exception)
        {
            await ReleaseRecognitionAsync();
            SetAvailability(await GetAvailabilityAsync());
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsListening && streamingRecognition is null)
        {
            return;
        }

        await ReleaseRecognitionAsync();
        ListeningStopped?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await ReleaseRecognitionAsync();

    private SpeechRecognitionAvailability GetAvailability()
    {
        if (!HasPackageIdentity())
        {
            return SpeechRecognitionAvailability.PackageIdentityRequired;
        }

        if (!IsRuntimeCompatible())
        {
            return SpeechRecognitionAvailability.Unsupported;
        }

        try
        {
            return SpeechRecognitionModel.GetReadyState() switch
            {
                AIFeatureReadyState.Ready => SpeechRecognitionAvailability.Ready,
                AIFeatureReadyState.NotReady => SpeechRecognitionAvailability.ModelRequired,
                AIFeatureReadyState.NotSupportedOnCurrentSystem => SpeechRecognitionAvailability.Unsupported,
                _ => SpeechRecognitionAvailability.Unavailable
            };
        }
        catch (Exception)
        {
            return SpeechRecognitionAvailability.Unavailable;
        }
    }

    private static bool IsRuntimeCompatible()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || AvxVnni.IsSupported)
        {
            return true;
        }

        try
        {
            return AICapabilities.HasAICapability(AICapabilityCategory.CopilotPlusPC);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<SpeechRecognitionAvailability> GetAvailabilityAsync()
    {
        Task<SpeechRecognitionAvailability> task;

        lock (availabilityLock)
        {
            task = availabilityTask ??= Task.Run(GetAvailability);
        }

        try
        {
            return await task.WaitAsync(AvailabilityTimeout);
        }
        catch (TimeoutException)
        {
            return SpeechRecognitionAvailability.Unavailable;
        }
        finally
        {
            if (task.IsCompleted)
            {
                lock (availabilityLock)
                {
                    if (ReferenceEquals(availabilityTask, task))
                    {
                        availabilityTask = null;
                    }
                }
            }
        }
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

    private async Task ReleaseRecognitionAsync()
    {
        StreamingRecognition? recognition = streamingRecognition;
        streamingRecognition = null;
        IsListening = false;

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

        await Task.CompletedTask;
    }

    private void HandleRecognizing(StreamingRecognition sender, StreamingRecognizingEventArgs args) => SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs(args.Text, false));

    private void HandleRecognized(StreamingRecognition sender, StreamingRecognizedEventArgs args) => SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs(args.Text, true));

    private void SetAvailability(SpeechRecognitionAvailability availability)
    {
        if (Availability == availability)
        {
            return;
        }

        Availability = availability;
        AvailabilityChanged?.Invoke(this, new SpeechRecognitionAvailabilityChangedEventArgs(availability));
    }
}
