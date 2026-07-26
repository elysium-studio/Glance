namespace Glance.SpeechToText;

public enum SpeechRecognitionAvailability
{
    Checking,
    Ready,
    ModelRequired,
    PackageIdentityRequired,
    Unsupported,
    Unavailable
}
