using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;

namespace Orleans.Runtime.MembershipService.SiloMetadata;

public static class SiloMetadataHostingExtensions
{
    public static ISiloBuilder UseSiloMetadata(this ISiloBuilder builder)
    {
        return builder.UseSiloMetadata(builder.Configuration);
    }

    public static ISiloBuilder UseSiloMetadata(this ISiloBuilder builder, IConfiguration configuration)
    {
        // Get the ORLEANS__METADATA section from config
        // Key/value pairs in configuration as a Dictionary <string, string> will look like this as environment variables:
        // ORLEANS__METADATA__key1=value1
        var metadataConfigSection = builder.Configuration.GetSection("ORLEANS").GetSection("METADATA");

        return builder.UseSiloMetadata(metadataConfigSection);
    }

    public static ISiloBuilder UseSiloMetadata(this ISiloBuilder builder, IConfigurationSection configurationSection)
    {
        var dictionary = configurationSection.Get<Dictionary<string, string>>();

        return builder.UseSiloMetadata(dictionary ?? new Dictionary<string, string>());
    }

    public static ISiloBuilder UseSiloMetadata(this ISiloBuilder builder, Dictionary<string, string> metadata)
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddOptionsWithValidateOnStart<global::Orleans.Runtime.MembershipService.SiloMetadata.SiloMetadata>()
                .Configure(m =>
                {
                    foreach (var data in metadata)
                    {
                        m.Metadata[data.Key] = data.Value;
                    }
                });

            services.AddOptionsWithValidateOnStart<SiloMetadataCacheOptions>();

            services
                .AddSingleton<SiloMetadataCache>()
                .AddGrainService<SiloMetadataGrainService>()
                .AddSingleton<ISiloMetadataClient, SiloMetadataClient>()
                ;
        });
        return builder;
    }
}
