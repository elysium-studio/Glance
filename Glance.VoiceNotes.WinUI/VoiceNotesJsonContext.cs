using System.Text.Json.Serialization;

namespace Glance.VoiceNotes.WinUI;

[JsonSerializable(typeof(VoiceNotesSettings))]
internal sealed partial class VoiceNotesJsonContext : JsonSerializerContext;
