using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans.Runtime.MembershipService.SiloMetadata;

namespace Orleans.Runtime.Placement;

public class SiloMetadataPlacementOptions
{
    // Collection of keys to use for metadata comparison.
    // It will attempt to match against all keys first.
    // If unable to find a match, then it will attempt to match against all keys except the first key.
    // Then it will attempt to match against all keys except the first and second keys.
    // And so on.
    // If no silos match on the final key, then it will return a random compatible silo.
    public string[] DefaultMetadataKeys { get; set; }

    public Dictionary<>
}

public class SiloMetadataPlacementDirector : IPlacementDirector
{
    private readonly ISiloMetadataClient _siloMetadataClient;
    private readonly SiloStatisticsCache _siloStatisticsCache;
    private readonly SiloAddress _localAddress;
    private readonly string[] _metadataValues;

    public SiloMetadataPlacementDirector(ILocalSiloDetails localSiloDetails, ISiloMetadataClient siloMetadataClient, SiloStatisticsCache siloStatisticsCache, IOptions<SiloMetadataPlacementOptions> options)
    {
        _siloMetadataClient = siloMetadataClient;
        _siloStatisticsCache = siloStatisticsCache;
        _localAddress = localSiloDetails.SiloAddress;
        _metadataValues = options.Value.DefaultMetadataKeys;
    }

    public async Task<SiloAddress> OnAddActivation(PlacementStrategy strategy, PlacementTarget target, IPlacementContext context)
    {
        var compatibleSilos =
            context.GetCompatibleSilos(target).Where(s =>
                !_siloStatisticsCache.TryGetStatistics(s, out var stats) || !stats.IsOverloaded)
                .ToArray();

        // If there are no compatible silos, just return the local silo.
        if (compatibleSilos.Length == 0)
        {
            return _localAddress;
        }

        // If a valid placement hint was specified, use it.
        if (IPlacementDirector.GetPlacementHint(target.RequestContextData, compatibleSilos) is { } placementHint)
        {
            return placementHint;
        }

        var localSiloMetadata = await _siloMetadataClient.GetSiloMetadata(_localAddress);
        var localMetadataValues = GetMetadataValues(localSiloMetadata);

        Random.Shared.Shuffle(compatibleSilos);

        // If we don't have a full match, then store the first instance of each partial match
        var fallbackSilos = new SiloAddress[_metadataValues.Length - 1];

        foreach (var silo in compatibleSilos)
        {
            var siloMetadata = await _siloMetadataClient.GetSiloMetadata(silo);
            var siloMetadataValues = GetMetadataValues(siloMetadata);
            if (localMetadataValues.SequenceEqual(siloMetadataValues))
            {
                return silo;
            }
            for (var i = 0; i < fallbackSilos.Length; i++)
            {
                if (fallbackSilos[i] is null && localMetadataValues.Skip(i + 1).SequenceEqual(siloMetadataValues.Skip(i + 1)))
                {
                    fallbackSilos[i] = silo;
                    break;
                }
            }
        }

        return fallbackSilos.FirstOrDefault(s => s is not null) ?? compatibleSilos[0];
    }

    private string[] GetMetadataValues(SiloMetadata localSiloMetadata) => _metadataValues.Select(key => localSiloMetadata.Metadata.GetValueOrDefault(key)).ToArray();
}