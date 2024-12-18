namespace Orleans.Runtime;

/// <summary>
/// A placement strategy which prefers placement on silos with matching metadata.
/// </summary>
/// <remarks>
/// <para>TODO: fill out</para>
/// <para>Silos which are overloaded by definition of the load shedding mechanism are not considered as candidates for new placements.</para>
/// <para><i>This placement strategy is configured by adding the <see cref="Placement.SiloMetadataPlacementAttribute"/> attribute to a grain.</i></para>
/// </remarks>
public sealed class SiloMetadataPlacement : PlacementStrategy
{
    internal static readonly SiloMetadataPlacement Singleton = new();
}