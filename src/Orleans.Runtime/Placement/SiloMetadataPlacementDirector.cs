using System;
using System.Collections.Concurrent;
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
}

internal class SiloMetadataPlacementDirector : IPlacementDirector
{
    private readonly ISiloMetadataClient _siloMetadataClient;
    private readonly ResourceOptimizedPlacementLogic _resourceOptimizedPlacementLogic;
    private readonly SiloStatisticsCache _siloStatisticsCache;
    private readonly SiloAddress _localAddress;
    private readonly string[] _metadataValues;

    internal SiloMetadataPlacementDirector(ILocalSiloDetails localSiloDetails, ISiloMetadataClient siloMetadataClient, ResourceOptimizedPlacementLogic resourceOptimizedPlacementLogic, SiloStatisticsCache siloStatisticsCache, IOptions<SiloMetadataPlacementOptions> options)
    {
        _siloMetadataClient = siloMetadataClient;
        _resourceOptimizedPlacementLogic = resourceOptimizedPlacementLogic;
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

        var candidateSilos = new List<SiloAddress>();
        for(var i = 0; i < _metadataValues.Length; i++)
        {
            candidateSilos.Clear();
            foreach (var silo in compatibleSilos)
            {
                var siloMetadata = await _siloMetadataClient.GetSiloMetadata(silo);
                var siloMetadataValues = GetMetadataValues(siloMetadata);
                if (localMetadataValues.AsSpan(i).SequenceEqual(siloMetadataValues.AsSpan(i)))
                {
                    candidateSilos.Add(silo);
                }
            }
            if (candidateSilos.Count > 0)
            {
                return await _resourceOptimizedPlacementLogic.PickSilo(context, candidateSilos: candidateSilos.ToArray());
            }
        }

        return await _resourceOptimizedPlacementLogic.PickSilo(context, candidateSilos: compatibleSilos);
    }

    private string[] GetMetadataValues(SiloMetadata localSiloMetadata) => _metadataValues.Select(key => localSiloMetadata.Metadata.GetValueOrDefault(key)).ToArray();
}


public class SiloStatisticsCache : ISiloStatisticsChangeListener
{
    // Track created activations on this silo between statistic intervals.
    private readonly ConcurrentDictionary<SiloAddress, SiloRuntimeStatistics> _localCache = new();

    internal SiloStatisticsCache(
        DeploymentLoadPublisher deploymentLoadPublisher)
    {
        deploymentLoadPublisher?.SubscribeToStatisticsChangeEvents(this);
    }
    public void SiloStatisticsChangeNotification(SiloAddress updatedSilo, SiloRuntimeStatistics newSiloStats) =>
        _localCache[updatedSilo] = newSiloStats;

    public void RemoveSilo(SiloAddress removedSilo) => _localCache.TryRemove(removedSilo, out _);

    public bool TryGetStatistics(SiloAddress silo, out SiloRuntimeStatistics statistics) => _localCache.TryGetValue(silo, out statistics);
}