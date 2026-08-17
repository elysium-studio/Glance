using Glance.Application.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandContentReader
{
    IReadOnlyList<GlanceContentKind> GetAvailableKinds(DataPackageView dataView);

    Task<GlanceContentContext?> ReadAsync(DataPackageView dataView, GlanceContentKind kind);
}
