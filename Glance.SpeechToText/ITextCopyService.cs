namespace Glance.SpeechToText;

public interface ITextCopyService
{
    Task CopyAsync(string text);
}
