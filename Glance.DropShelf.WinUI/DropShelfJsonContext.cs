using System.Text.Json.Serialization;

namespace Glance.DropShelf.WinUI;

[JsonSerializable(typeof(DropShelfSettings))]
internal sealed partial class DropShelfJsonContext : JsonSerializerContext;
