namespace Glance.SpeechToText;

public enum SpeechToTextState
{
    Loading,
    ModelRequired,
    Ready,
    Starting,
    Listening,
    Paused,
    Error
}
