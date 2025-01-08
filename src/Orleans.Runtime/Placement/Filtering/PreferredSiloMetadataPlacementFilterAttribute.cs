using System;
using System.Collections.Generic;
using Orleans.Runtime;

namespace Orleans.Placement;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class PreferredSiloMetadataPlacementFilterAttribute : PlacementFilterAttribute
{
    public PreferredSiloMetadataPlacementFilterAttribute(string[] orderedMetadataKeys)
        : base(new PreferredSiloMetadataPlacementFilter(orderedMetadataKeys))
    {
    }

    public override void Populate(IServiceProvider services, Type grainClass, GrainType grainType, Dictionary<string, string> properties)
    {

        //properties[WellKnownGrainTypeProperties.PlacementFilter] = this.PlacementFilter.GetType().Name;
        base.Populate(services, grainClass, grainType, properties);
    }
}