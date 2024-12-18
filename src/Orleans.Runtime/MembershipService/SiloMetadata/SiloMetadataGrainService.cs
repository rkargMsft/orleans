using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Services;

namespace Orleans.Runtime.MembershipService.SiloMetadata;

[GenerateSerializer]
public record SiloMetadata
{
    [Id(0)]
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public interface ISiloMetadataGrainService : IGrainService
{
    Task<SiloMetadata> GetSiloMetadata();
}

public class SiloMetadataGrainService : GrainService, ISiloMetadataGrainService
{
    private readonly SiloMetadata _siloMetadata;

    public SiloMetadataGrainService(IOptions<SiloMetadata> siloMetadata) : base()
    {
        _siloMetadata = siloMetadata.Value;
    }

    public SiloMetadataGrainService(IOptions<SiloMetadata> siloMetadata, GrainId grainId, Silo silo, ILoggerFactory loggerFactory) : base(grainId, silo, loggerFactory)
    {
        _siloMetadata = siloMetadata.Value;
    }

    public Task<SiloMetadata> GetSiloMetadata()
    {
        return Task.FromResult(_siloMetadata);
    }
}
