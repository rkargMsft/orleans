using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Placement;
using Orleans.Runtime.Placement;
using Orleans.Storage;
using Orleans.TestingHost;
using TestExtensions;
using UnitTests.GrainInterfaces;
using Xunit;

namespace UnitTests.General
{
    [CollectionDefinition("ActivityPropagationTests Collection")]
    public class ActivityPropagationTestsCollection : ICollectionFixture<ActivityPropagationTests.Fixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    [Collection("ActivityPropagationTests Collection")]
    public class ActivityPropagationTests : OrleansTestingBase, IClassFixture<ActivityPropagationTests.Fixture>, IDisposable
    {
        public class Fixture : BaseTestClusterFixture
        {
            protected override void ConfigureTestCluster(TestClusterBuilder builder)
            {
                builder.ConfigureHostConfiguration(TestDefaultConfiguration.ConfigureHostConfiguration);
                builder.AddSiloBuilderConfigurator<SiloInvokerTestSiloBuilderConfigurator>();
                builder.AddClientBuilderConfigurator<ClientConfigurator>();
            }

            private class SiloInvokerTestSiloBuilderConfigurator : ISiloConfigurator
            {
                public void Configure(ISiloBuilder hostBuilder) =>
                    hostBuilder
                        .AddActivityPropagation()
                        .AddMemoryGrainStorageAsDefault()
                        .AddMemoryGrainStorage("PubSubStore")
                        .ConfigureServices(collection =>
                        {
                            collection
                                .AddPlacementDirector<TestCustomPlacementStrategy,
                                    TestPlacementStrategyFixedSiloDirector>();
                        });
            }

            private class ClientConfigurator : IClientBuilderConfigurator
            {
                public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) =>
                    clientBuilder
                        .AddActivityPropagation();
            }
        }

        private readonly ActivityIdFormat defaultIdFormat;
        private readonly Fixture fixture;
        private readonly ActivityListener _listener;
        private readonly List<Activity> _activities = new();

        public ActivityPropagationTests(Fixture fixture)
        {
            defaultIdFormat = Activity.DefaultIdFormat;
            this.fixture = fixture;

            _listener = new()
            {
                ShouldListenTo = p => p.Name == ActivitySources.ApplicationGrainActivitySourceName,
                Sample = Sample,
                SampleUsingParentId = SampleUsingParentId,
                ActivityStarted = activity => _activities.Add(activity),
            };

            static ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
            {
                //Trace id has to be accessed in sample to reproduce the scenario when SetParentId does not work
                var _ = options.TraceId;
                return ActivitySamplingResult.AllDataAndRecorded;
            };

            static ActivitySamplingResult SampleUsingParentId(ref ActivityCreationOptions<string> options)
            {
                //Trace id has to be accessed in sample to reproduce the scenario when SetParentId does not work
                var _ = options.TraceId;
                return ActivitySamplingResult.AllDataAndRecorded;
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();

        [Theory]
        [InlineData(ActivityIdFormat.W3C)]
        [InlineData(ActivityIdFormat.Hierarchical)]
        [TestCategory("BVT")]
        public async Task WithoutParentActivity(ActivityIdFormat idFormat)
        {
            Activity.DefaultIdFormat = idFormat;

            await Test(fixture.GrainFactory);
            await Test(fixture.Client);

            static async Task Test(IGrainFactory grainFactory)
            {
                var grain = grainFactory.GetGrain<IActivityGrain>(Random.Shared.Next());

                var result = await grain.GetActivityId();

                Assert.NotNull(result);
                Assert.NotEmpty(result.Id);
                Assert.Null(result.TraceState);
            }
        }

        [Fact]
        [TestCategory("BVT")]
        public async Task WithParentActivity_W3C()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            var activity = new Activity("SomeName");
            activity.TraceStateString = "traceState";
            activity.AddBaggage("foo", "bar");
            activity.Start();

            try
            {
                await Test(fixture.GrainFactory);
                await Test(fixture.Client);
            }
            finally
            {
                activity.Stop();
            }

            async Task Test(IGrainFactory grainFactory)
            {
                var grain = grainFactory.GetGrain<IActivityGrain>(Random.Shared.Next());

                var result = await grain.GetActivityId();

                Assert.NotNull(result);
                Assert.NotNull(result.Id);
                Assert.Contains(activity.TraceId.ToHexString(), result.Id); // ensure, that trace id is persisted.
                Assert.Equal(activity.TraceStateString, result.TraceState);
                Assert.Equal(activity.Baggage, result.Baggage);
            }
        }

        [Fact]
        [TestCategory("BVT")]
        public async Task WithParentActivity_Hierarchical()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.Hierarchical;

            var activity = new Activity("SomeName");
            activity.AddBaggage("foo", "bar");
            activity.Start();

            try
            {
                await Test(fixture.GrainFactory);
                await Test(fixture.Client);
            }
            finally
            {
                activity.Stop();
            }

            async Task Test(IGrainFactory grainFactory)
            {
                var grain = grainFactory.GetGrain<IActivityGrain>(Random.Shared.Next());

                var result = await grain.GetActivityId();

                Assert.NotNull(result);
                Assert.NotNull(result.Id);
                Assert.StartsWith(activity.Id, result.Id);
                Assert.Equal(activity.Baggage, result.Baggage);
            }
        }

        [Fact]
        [TestCategory("BVT")]
        public async Task MessageCallSpans()
        {
            // Arrange
            var activityGrain = fixture.GrainFactory.GetGrain<IActivityGrain>(Random.Shared.Next());

            // Act
            await activityGrain.GetActivityId();

            // Assert
            // IActivityGrain/GetActivityId(Client)
            // └── Activate(Internal)
            //     └── OnStart(Internal)
            //         └── IActivityGrain/GetActivityId(Server)

            var clientCallActivity = _activities.SingleOrDefault(a => a.OperationName == "IActivityGrain/GetActivityId" && a.Kind == ActivityKind.Client);
            Assert.NotNull(clientCallActivity);

            var activateActivity = _activities.SingleOrDefault(a => a.OperationName == "Activate" && a.Kind == ActivityKind.Internal);
            Assert.NotNull(activateActivity);
            Assert.Equal(clientCallActivity.SpanId, activateActivity.ParentSpanId);

            var onStartActivity = _activities.SingleOrDefault(a => a.OperationName == "OnStart" && a.Kind == ActivityKind.Internal);
            Assert.NotNull(onStartActivity);
            Assert.Equal(activateActivity.SpanId, onStartActivity.ParentSpanId);

            var serverCallActivity = _activities.SingleOrDefault(a => a.OperationName == "IActivityGrain/GetActivityId" && a.Kind == ActivityKind.Server);
            Assert.NotNull(serverCallActivity);
            Assert.Equal(clientCallActivity.SpanId, serverCallActivity.ParentSpanId);
        }

        [Fact]
        [TestCategory("BVT")]
        public async Task PersistentStateSpan()
        {
            // Arrange
            var activityGrain = fixture.GrainFactory.GetGrain<IOnActivateGrain>(Random.Shared.Next());

            // Act
            _ = await activityGrain.GetState();

            // Assert
            Assert.Equal(5, _activities.Count(a => a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString()))));
            Assert.Contains(_activities, a => a.OperationName == "Activate" && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
            Assert.Contains(_activities, a => a.OperationName == "OnStart" && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
            Assert.Contains(_activities, a => a.OperationName == "OnActivateAsync" && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
            Assert.Contains(_activities, a => a.OperationName == "IOnActivateGrain/GetState" && a.Kind == ActivityKind.Client && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
            Assert.Contains(_activities, a => a.OperationName == "IOnActivateGrain/GetState" && a.Kind == ActivityKind.Server && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));

            _activities.Clear();

            // Act
            _ = await activityGrain.GetState();

            // Assert
            Assert.Equal(2, _activities.Count(a => a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString()))));
            Assert.Contains(_activities, a => a.OperationName == "IOnActivateGrain/GetState" && a.Kind == ActivityKind.Client && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
            Assert.Contains(_activities, a => a.OperationName == "IOnActivateGrain/GetState" && a.Kind == ActivityKind.Server && a.Tags.Contains(new KeyValuePair<string, string>("rpc.orleans.target_id", activityGrain.GetGrainId().ToString())));
        }

        [Fact]
        [TestCategory("BVT")]
        public async Task MigrationGrainSpans()
        {
            // Arrange
            var activityGrain = fixture.GrainFactory.GetGrain<IMigrationTestGrain>(Random.Shared.Next());
            
            await activityGrain.Migrate();

            var deactivationDuration = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(5);
            var success = MigrationTestGrain.Deactivated.WaitOne(deactivationDuration);
            Assert.True(success, "Migration never triggered");

            var startDeactivating = _activities.SingleOrDefault(a => a.OperationName == "StartDeactivating");
            Assert.NotNull(startDeactivating);

            var finishDeactivating = _activities.SingleOrDefault(a => a.OperationName == "FinishDeactivating");
            Assert.NotNull(finishDeactivating);

            var dehydrate = _activities.SingleOrDefault(a => a.OperationName == "Dehydrate");
            Assert.NotNull(dehydrate);
            Assert.Equal(startDeactivating.ParentSpanId, finishDeactivating.ParentSpanId);
            Assert.Equal(finishDeactivating.SpanId, dehydrate.ParentSpanId);

            _activities.Clear();
            // Act
            await activityGrain.MethodCall();

            var rehydrate = _activities.SingleOrDefault(a => a.OperationName == "Rehydrate");
            Assert.NotNull(rehydrate);

            var activate = _activities.SingleOrDefault(a => a.OperationName == "Activate");
            Assert.NotNull(activate);

            var onStart = _activities.SingleOrDefault(a => a.OperationName == "OnStart");
            Assert.NotNull(onStart);

            var onActivateAsync = _activities.SingleOrDefault(a => a.OperationName == "OnActivateAsync");
            Assert.NotNull(onActivateAsync);

            var methodCallClient = _activities.SingleOrDefault(a => a.OperationName == "IMigrationTestGrain/MethodCall" && a.Kind == ActivityKind.Client);
            Assert.NotNull(methodCallClient);

            var methodCallServer = _activities.SingleOrDefault(a => a.OperationName == "IMigrationTestGrain/MethodCall" && a.Kind == ActivityKind.Server);
            Assert.NotNull(methodCallServer);

            var migrateCallClient = _activities.SingleOrDefault(a => a.OperationName == "IActivationMigrationManagerSystemTarget/AcceptMigratingGrains" && a.Kind == ActivityKind.Client);
            Assert.NotNull(migrateCallClient);

            var migrateCallServer = _activities.SingleOrDefault(a => a.OperationName == "IActivationMigrationManagerSystemTarget/AcceptMigratingGrains" && a.Kind == ActivityKind.Server);
            Assert.NotNull(migrateCallServer);

            Assert.Equal(activate.SpanId, onStart.ParentSpanId);
            Assert.Equal(activate.SpanId, onActivateAsync.ParentSpanId);
            Assert.Equal(migrateCallServer.SpanId, rehydrate.ParentSpanId);
            Assert.Equal(migrateCallClient.SpanId, migrateCallServer.ParentSpanId);
            Assert.Equal(activate.SpanId, onStart.ParentSpanId);
            Assert.Equal(activate.SpanId, onStart.ParentSpanId);

            Assert.Contains(_activities, a => a.OperationName == "Rehydrate");
            Assert.Contains(_activities, a => a.OperationName == "Activate");
            Assert.Contains(_activities, a => a.OperationName == "OnStart");
            Assert.Contains(_activities, a => a.OperationName == "OnActivateAsync");
            Assert.Contains(_activities, a => a.OperationName == "IMigrationTestGrain/MethodCall");
            Assert.Contains(_activities, a => a.OperationName == "IMigrationTestGrain/MethodCall");
        }
    }

    public interface IMigrationTestGrain : IGrainWithIntegerKey
    {
        public Task Migrate();
        public Task MethodCall();
    }

    [TestPlacementStrategy(CustomPlacementScenario.DifferentSilo)]
    public class MigrationTestGrain : Grain, IMigrationTestGrain, IGrainMigrationParticipant
    {
        public static ManualResetEvent Deactivated = new ManualResetEvent(false);

        public Task Migrate()
        {
            MigrateOnIdle();
            return Task.CompletedTask;
        }

        public Task MethodCall() => Task.CompletedTask;

        public void OnDehydrate(IDehydrationContext dehydrationContext)
        {
            Deactivated.Set();
        }

        public void OnRehydrate(IRehydrationContext rehydrationContext)
        {
        }
    }
}
