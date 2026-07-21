namespace Glance.PrivacyControls;

public sealed class MicrophoneLevelChangedEventArgs(double level) :
    EventArgs
{
    public double Level { get; } = level;
}
