using System.Text.Json.Serialization;

namespace Glance.VoiceNotes.WinUI;

[JsonSerializable(typeof(VoiceNotesSettings))]
internal partial class VoiceNotesJsonContext : JsonSerializerContext;
