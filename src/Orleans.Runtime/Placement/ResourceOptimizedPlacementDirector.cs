#nullable enable
using System.Threading.Tasks;

namespace Orleans.Runtime.Placement;

// See: https://www.ledjonbehluli.com/posts/orleans_resource_placement_kalman/
internal sealed class ResourceOptimizedPlacementDirector : IPlacementDirector
{
    private readonly ResourceOptimizedPlacementLogic _internals;

    public ResourceOptimizedPlacementDirector(
        ResourceOptimizedPlacementLogic internals)
    {
        _internals = internals;
    }

    public Task<SiloAddress> OnAddActivation(PlacementStrategy strategy, PlacementTarget target, IPlacementContext context)
    {
        var compatibleSilos = context.GetCompatibleSilos(target);

        if (IPlacementDirector.GetPlacementHint(target.RequestContextData, compatibleSilos) is { } placementHint)
        {
            return Task.FromResult(placementHint);
        }

        if (compatibleSilos.Length == 0)
        {
            throw new SiloUnavailableException($"Cannot place grain '{target.GrainIdentity}' because there are no compatible silos.");
        }

        return _internals.PickSilo(context, compatibleSilos);
    }
}