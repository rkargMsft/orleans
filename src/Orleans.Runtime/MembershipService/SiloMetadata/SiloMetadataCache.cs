using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orleans.Runtime.MembershipService.SiloMetadata;

public class SiloMetadataCacheOptions
{
    public TimeSpan CachePurgeInterval { get; set; } = TimeSpan.FromMinutes(10);
}

public class SiloMetadataCache : IDisposable
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SiloMetadataCache> _logger;
    private readonly IOptions<SiloMetadataCacheOptions> _cacheOptions;
    private readonly ConcurrentDictionary<SiloAddress, SiloMetadata> _metadata = new();
    private readonly CancellationTokenSource _cts = new();
    private PeriodicTimer _pt;

    public SiloMetadataCache(IClusterClient clusterClient, ILogger<SiloMetadataCache> logger, IOptions<SiloMetadataCacheOptions> cacheOptions)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _cacheOptions = cacheOptions;
        PurgeMetadataCache().Ignore();
    }

    private async Task PurgeMetadataCache()
    {
        _pt = new PeriodicTimer(_cacheOptions.Value.CachePurgeInterval);
        while (!_cts.IsCancellationRequested)
        {
            await _pt.WaitForNextTickAsync(_cts.Token);

            Dictionary<SiloAddress, SiloStatus> activeSilos;
            try
            {
                var managementClient = _clusterClient.GetGrain<IManagementGrain>(0);
                activeSilos = await managementClient.GetHosts(true);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get active silos to purge metadata cache. This won't impact the data correctness but there may be extra metadata in memory from defunct silos.");
                continue;
            }

            foreach (var silo in _metadata.Keys.ToList())
            {
                if (!activeSilos.ContainsKey(silo))
                {
                    _metadata.TryRemove(silo, out _);
                }
            }
        }
    }

    public SiloMetadata GetMetadata(SiloAddress siloAddress) => _metadata.GetValueOrDefault(siloAddress);

    public void SetMetadata(SiloAddress siloAddress, SiloMetadata metadata) => _metadata.TryAdd(siloAddress, metadata);

    public void Dispose()
    {
        _cts.Cancel();
        _pt.Dispose();
    }
}
