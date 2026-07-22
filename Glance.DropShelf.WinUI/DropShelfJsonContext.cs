using System.Text.Json.Serialization;

namespace Glance.DropShelf.WinUI;

[JsonSerializable(typeof(DropShelfSettings))]
internal partial class DropShelfJsonContext : JsonSerializerContext;
