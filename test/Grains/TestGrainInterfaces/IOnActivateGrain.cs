namespace UnitTests.GrainInterfaces;

public interface IOnActivateGrain: IGrainWithIntegerKey
{
    Task<SomeState> GetState();
}

public class OnActivateGrain : Grain<SomeState>, IOnActivateGrain
{
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SomeState> GetState() => Task.FromResult(State);
}

[GenerateSerializer]
public class SomeState
{
    [Id(0)]
    public int SomeData { get; set; }
}
