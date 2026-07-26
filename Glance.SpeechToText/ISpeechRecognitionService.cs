namespace Glance.SpeechToText;

public interface ISpeechRecognitionService :
    IAsyncDisposable
{
    SpeechRecognitionAvailability Availability { get; }

    bool IsListening { get; }

    event EventHandler<SpeechRecognitionAvailabilityChangedEventArgs>? AvailabilityChanged;

    event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;

    event EventHandler? ListeningStopped;

    Task CheckAvailabilityAsync();

    Task<bool> EnsureModelAsync();

    Task<bool> StartAsync(SpeechAudioSource audioSource);

    Task StopAsync();
}
