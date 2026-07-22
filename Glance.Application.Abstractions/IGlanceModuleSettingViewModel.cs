namespace Glance.Application.Abstractions;

public interface IGlanceModuleSettingViewModel :
    IDisposable
{
    string ModuleId { get; }

    int Order { get; }
}
