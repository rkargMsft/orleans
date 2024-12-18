using System;
using System.Collections.Generic;
using System.Linq;
using Orleans.Metadata;
using Orleans.Runtime;
using Orleans.Runtime.MembershipService.SiloMetadata;
using Orleans.Runtime.Placement;

namespace Orleans.Placement;

public interface IFilterDirector
{
    IEnumerable<SiloAddress> Filter(PlacementFilter filter, PlacementTarget target,
        IEnumerable<SiloAddress> silos);
}
public class PreferredSiloMetadataFilterDirector : IFilterDirector
{
    private readonly ILocalSiloDetails _localSiloDetails;
    private readonly SiloMetadataCache _siloMetadataCache;

    public PreferredSiloMetadataFilterDirector(ILocalSiloDetails localSiloDetails, SiloMetadataCache siloMetadata)
    {
        _localSiloDetails = localSiloDetails;
        _siloMetadataCache = siloMetadata;
    }

    public IEnumerable<SiloAddress> Filter(PlacementFilter filter, PlacementTarget target, IEnumerable<SiloAddress> silos)
    {
        var orderedMetadataKeys = (filter as PreferredSiloMetadataPlacementFilter).OrderedMetadataKeys;
        var localSiloMetadata = _siloMetadataCache.GetMetadata(_localSiloDetails.SiloAddress)?.Metadata;

        if (localSiloMetadata is null)
        {
            // yield return all silos if no silos match any metadata keys
            foreach (var silo in silos)
            {
                yield return silo;
            }
        }
        else
        {
            // return the list of silos that match the most metadata keys. The first key in the list is the least important.
            // This means that the last key in the list is the most important.
            // If no silos match any metadata keys, return the original list of silos.

            var siloList = silos.ToList();
            var maxScore = 0;
            var siloScores = new int[siloList.Count];
            for (int i = 0; i < siloList.Count; i++)
            {
                var siloMetadata = _siloMetadataCache.GetMetadata(siloList[i]).Metadata;
                for (int j = orderedMetadataKeys.Length - 1; j >= 0; --j)
                {
                    if (siloMetadata.TryGetValue(orderedMetadataKeys[j], out var siloMetadataValue) &&
                        localSiloMetadata.TryGetValue(orderedMetadataKeys[j], out var localSiloMetadataValue) &&
                        siloMetadataValue == localSiloMetadataValue)
                    {
                        var newScore = siloScores[i]++;
                        maxScore = Math.Max(maxScore, newScore);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (maxScore == 0)
            {
                // yield return all silos if no silos match any metadata keys
                foreach (var silo in siloList)
                {
                    yield return silo;
                }
            }

            for (var i = 0; i < siloScores.Length; i++)
            {
                if (siloScores[i] == maxScore)
                {
                    yield return siloList[i];
                }
            }
        }
    }
}
public class RequiredSiloMetadataFilterDirector : IFilterDirector
{
    private readonly ILocalSiloDetails _localSiloDetails;
    private readonly SiloMetadataCache _siloMetadataCache;

    public RequiredSiloMetadataFilterDirector(ILocalSiloDetails localSiloDetails, SiloMetadataCache siloMetadata)
    {
        _localSiloDetails = localSiloDetails;
        _siloMetadataCache = siloMetadata;
    }

    public IEnumerable<SiloAddress> Filter(PlacementFilter filter, PlacementTarget target, IEnumerable<SiloAddress> silos)
    {
        var orderedMetadataKeys = (filter as RequiredSiloMetadataPlacementFilter);
        // yield return all silos if no silos match any metadata keys
        foreach (var silo in silos)
        {
            yield return silo;
        }
    }
}
public class PreferredSiloMetadataPlacementFilter : PlacementFilter
{
    public string[] OrderedMetadataKeys { get; set; }

    public PreferredSiloMetadataPlacementFilter() : this([])
    {
    }

    public PreferredSiloMetadataPlacementFilter(string[] orderedMetadataKeys)
    {
        OrderedMetadataKeys = orderedMetadataKeys;
    }

    public override void Initialize(GrainProperties properties)
    {
        base.Initialize(properties);
        OrderedMetadataKeys = GetPlacementFilterGrainProperty("ordered-metadata-keys", properties).Split(",");
    }

    protected override IEnumerable<KeyValuePair<string, string>> GetAdditionalGrainProperties(IServiceProvider services, Type grainClass, GrainType grainType,
        IReadOnlyDictionary<string, string> existingProperties)
    {
        yield return new KeyValuePair<string, string>("ordered-metadata-keys",
            string.Join(",", OrderedMetadataKeys));
    }
}