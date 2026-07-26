namespace Glance.SpeechToText;

public sealed class SpeechRecognizedEventArgs(string text, bool isFinal) :
    EventArgs
{
    public string Text { get; } = text;

    public bool IsFinal { get; } = isFinal;
}
