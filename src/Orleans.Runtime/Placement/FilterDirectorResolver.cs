using System;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Placement;

namespace Orleans.Runtime.Placement;

/// <summary>
/// Responsible for resolving an <see cref="IFilterDirector"/> for a <see cref="PlacementFilter"/>.
/// </summary>
public sealed class FilterDirectorResolver
{
    private readonly IServiceProvider _services;

    public FilterDirectorResolver(IServiceProvider services)
    {
        _services = services;
    }

    public IFilterDirector GetFilterDirector(PlacementFilter placementFilter) => _services.GetRequiredKeyedService<IFilterDirector>(placementFilter.GetType());
}