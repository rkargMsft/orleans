using System;
using System.Collections.Generic;
using Orleans.Runtime;

namespace Orleans.Placement;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RequiredSiloMetadataPlacementFilterAttribute : PlacementFilterAttribute
{
    public RequiredSiloMetadataPlacementFilterAttribute(string[] orderedMetadataKeys)
        : base(new RequiredSiloMetadataPlacementFilter(orderedMetadataKeys))
    {
    }

    public override void Populate(IServiceProvider services, Type grainClass, GrainType grainType, Dictionary<string, string> properties)
    {
        //properties[WellKnownGrainTypeProperties.PlacementFilter] = this.PlacementFilter.GetType().Name;
        base.Populate(services, grainClass, grainType, properties);
    }
}