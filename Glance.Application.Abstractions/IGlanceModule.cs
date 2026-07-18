using Microsoft.Extensions.DependencyInjection;

namespace Glance.Application.Abstractions;

public interface IGlanceModule
{
    void Register(IServiceCollection services);
}
