namespace Glance.SpeechToText;

public sealed class SpeechRecognitionAvailabilityChangedEventArgs(SpeechRecognitionAvailability availability) :
    EventArgs
{
    public SpeechRecognitionAvailability Availability { get; } = availability;
}
