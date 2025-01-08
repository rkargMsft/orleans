using System;
using System.Collections.Generic;
using Orleans.Metadata;
using Orleans.Runtime;

namespace Orleans.Placement;

/// <summary>
/// Base for all placement filter marker attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class PlacementFilterAttribute : Attribute, IGrainPropertiesProviderAttribute
{
    public PlacementFilter PlacementFilter { get; private set; }

    protected PlacementFilterAttribute(PlacementFilter placement)
    {
        if (placement == null) throw new ArgumentNullException(nameof(placement));

        this.PlacementFilter = placement;
    }

    /// <inheritdoc />
    public virtual void Populate(IServiceProvider services, Type grainClass, GrainType grainType, Dictionary<string, string> properties)
    {
        this.PlacementFilter?.PopulateGrainProperties(services, grainClass, grainType, properties);
    }
}