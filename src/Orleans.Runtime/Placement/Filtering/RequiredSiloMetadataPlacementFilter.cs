using System;
using System.Collections.Generic;
using Orleans.Metadata;
using Orleans.Runtime;

namespace Orleans.Placement;

public class RequiredSiloMetadataPlacementFilter : PlacementFilter
{
    private string[] _metadataKeys;

    public RequiredSiloMetadataPlacementFilter() : this([])
    {
    }

    public RequiredSiloMetadataPlacementFilter(string[] metadataKeys)
    {
        _metadataKeys = metadataKeys;
    }

    public override void Initialize(GrainProperties properties)
    {
        base.Initialize(properties);
        _metadataKeys = GetPlacementFilterGrainProperty("metadata-keys", properties).Split(",");
    }

    protected override IEnumerable<KeyValuePair<string, string>> GetAdditionalGrainProperties(IServiceProvider services, Type grainClass, GrainType grainType,
        IReadOnlyDictionary<string, string> existingProperties)
    {
        yield return new KeyValuePair<string, string>("metadata-keys", String.Join(",", _metadataKeys));
    }
}