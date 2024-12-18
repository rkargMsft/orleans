using System;
using System.Threading.Tasks;
using Orleans.Runtime.Services;
using Orleans.Services;

namespace Orleans.Runtime.MembershipService.SiloMetadata;

public interface ISiloMetadataClient : IGrainServiceClient<ISiloMetadataGrainService>
{
    public ValueTask<SiloMetadata> GetSiloMetadata(SiloAddress siloAddress);
}

public class SiloMetadataClient : GrainServiceClient<ISiloMetadataGrainService>, ISiloMetadataClient
{
    private readonly SiloMetadataCache _cache;

    public SiloMetadataClient(SiloMetadataCache cache, IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _cache = cache;
    }

    public ValueTask<SiloMetadata> GetSiloMetadata(SiloAddress siloAddress)
    {
        var cached = _cache.GetMetadata(siloAddress);
        if (cached is not null)
        {
            return ValueTask.FromResult(cached);
        }

        return new ValueTask<SiloMetadata>(SlowGetSiloMetadata(siloAddress));
    }

    private async Task<SiloMetadata> SlowGetSiloMetadata(SiloAddress siloAddress)
    {
        var grainService = GetGrainService(siloAddress);
        var metadata = await grainService.GetSiloMetadata();
        _cache.SetMetadata(siloAddress, metadata);
        return metadata;
    }
}
