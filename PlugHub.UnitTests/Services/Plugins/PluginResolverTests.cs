using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services.Plugins;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;


namespace PlugHub.UnitTests.Services.Plugins
{
    [DescriptorProvider("GetTestForwardDescriptors", sortContext: DescriptorSortContext.Forward)]
    internal interface ITestForwardInterface
    {
        public IEnumerable<TestPluginDescriptor> GetTestForwardDescriptors();
    }
    internal class TestForwardPlugin(IEnumerable<TestPluginDescriptor> descriptors) : ITestForwardInterface
    {
        private readonly IEnumerable<TestPluginDescriptor> descriptors = descriptors;

        public IEnumerable<TestPluginDescriptor> GetTestForwardDescriptors() => this.descriptors;
    }

    [DescriptorProvider("GetTestReverseDescriptors")]
    internal interface ITestReverseInterface
    {
        public IEnumerable<TestPluginDescriptor> GetTestReverseDescriptors();
    }
    internal class TestReversePlugin(IEnumerable<TestPluginDescriptor> descriptors) : ITestReverseInterface
    {
        private readonly IEnumerable<TestPluginDescriptor> descriptors = descriptors;

        public IEnumerable<TestPluginDescriptor> GetTestReverseDescriptors() => this.descriptors;
    }

    internal record TestPluginDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) : PluginDescriptor(
            PluginID,
            InterfaceID,
            Version,
            LoadBefore,
            LoadAfter,
            DependsOn,
            ConflictsWith);

    [TestClass]
    public sealed class PluginResolverTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private PluginResolver? pluginResolver;

        [TestInitialize]
        public void Setup()
        {
            this.pluginResolver = new PluginResolver(new NullLogger<PluginResolver>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            Thread.Sleep(100);
            Serilog.Log.CloseAndFlush();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            this.msTestHelpers!.Dispose();
        }

        #region PluginResolverTests: Constructor

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_ValidLogger_CreatesInstance()
        {
            // Arrange & Act
            PluginResolver resolver = new(new NullLogger<PluginResolver>());

            // Assert
            Assert.IsInstanceOfType<PluginResolver>(resolver, "PluginResolver should be created successfully");
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new PluginResolver(null!));
        }

        #endregion

        #region PluginResolverTests: Basic Resolution

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_EmptyCollectionReturnsEmpty()
        {
            // Arrange
            List<TestPluginDescriptor> descriptors = [];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(0, result.Count(), "Should return empty collection for empty input");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_SingleDescriptorReturnsSingle()
        {
            // Arrange
            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor("Plugin1", "1.0.0")
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(1, result.Count(), "Should return single descriptor");
            Assert.AreEqual(descriptors[0].PluginID, result.First().PluginID, "Should return the correct descriptor");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_MultipleIndependentDescriptorsReturnsAll()
        {
            // Arrange
            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor("Plugin1", "1.0.0"),
                CreateTestDescriptor("Plugin2", "1.0.0"),
                CreateTestDescriptor("Plugin3", "1.0.0")
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(3, result.Count(), "Should return all independent descriptors");

            HashSet<Guid> returnedIds = [.. result.Select(d => d.PluginID)];

            Assert.IsTrue(returnedIds.Contains(descriptors[0].PluginID), "Should contain Plugin1");
            Assert.IsTrue(returnedIds.Contains(descriptors[1].PluginID), "Should contain Plugin2");
            Assert.IsTrue(returnedIds.Contains(descriptors[2].PluginID), "Should contain Plugin3");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_DuplicateDescriptorIDFiltersOutDuplicates()
        {
            // Arrange
            Guid sharedInterfaceId = Guid.NewGuid();
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            string version = "1.0.0";

            // Create two descriptors with the same InterfaceID (duplicate)
            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, sharedInterfaceId, version),
                CreateTestDescriptor(plugin2Id, sharedInterfaceId, version) // duplicate
            ];

            // Act
            PluginResolutionContext<TestPluginDescriptor> context = this.pluginResolver!.ResolveContext(descriptors);

            // Assert
            Assert.AreEqual(2, descriptors.Count, "Original input contains two descriptors");
            Assert.AreEqual(1, context.DuplicateIDDisabled.Count, "One duplicate should be tracked as disabled");
            Assert.AreEqual(1, context.IdToDescriptor.Count, "One descriptor with unique InterfaceID should remain");

            IEnumerable<TestPluginDescriptor> result = this.pluginResolver.ResolveDescriptors(descriptors);
            Assert.AreEqual(1, result.Count(), "Duplicate descriptors should be filtered out from final resolution");
        }

        #endregion

        #region PluginResolverTests: Dependency Resolution

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_MissingDependencyFiltersOutInvalidDescriptor()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid missingId = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();
            Guid missingInterfaceId = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(missingId, missingInterfaceId, "1.0.0", "2.0.0")])
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(1, result.Count(), "Should return only valid descriptors");
            Assert.AreEqual(plugin1Id, result.First().PluginID, "Should return only Plugin1");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_VersionMismatchFiltersOutIncompatibleDescriptor()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "3.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")])
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(1, result.Count(), "Should return only compatible descriptors");
            Assert.AreEqual(descriptors[0].PluginID, result.First().PluginID, "Should return Plugin1 (even though incompatible)");
        }

        #endregion

        #region PluginResolverTests: Conflict Resolution

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_ConflictingDescriptorsFiltersOutConflicted()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", conflictsWith: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")])
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(1, result.Count(), "Should filter out conflicting descriptors");
            Assert.AreEqual(plugin1Id, result.First().PluginID, "Should keep Plugin1 and remove conflicting Plugin2");
        }

        #endregion

        #region PluginResolverTests: Load Ordering

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_LoadBeforeConstraintRespectsOrdering()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", loadBefore: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")]),
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0")
            ];

            // Act
            IList<TestPluginDescriptor> result = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];

            // Assert
            Assert.AreEqual(2, result.Count, "Should return both descriptors");
            Assert.AreEqual(plugin2Id, result[0].PluginID, "Plugin2 should load first (LoadBefore)");
            Assert.AreEqual(plugin1Id, result[1].PluginID, "Plugin1 should load second");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_LoadAfterConstraintRespectsOrdering()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", loadAfter: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")])
            ];

            // Act
            IList<TestPluginDescriptor> result = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];

            // Assert
            Assert.AreEqual(2, result.Count, "Should return both descriptors");
            Assert.AreEqual(plugin1Id, result[0].PluginID, "Plugin1 should load first");
            Assert.AreEqual(plugin2Id, result[1].PluginID, "Plugin2 should load second (LoadAfter)");
        }

        #endregion

        #region PluginResolverTests: SortContext

        [TestMethod]
        [TestCategory("ResolveAndOrder")]
        public void ResolveAndOrder_ForwardContext_ReturnsInForwardOrder()
        {
            // Arrange
            IReadOnlyList<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1.0.0"),
                CreateTestDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1.0.0")
            ];

            TestForwardPlugin plugin = new(descriptors);

            // Act
            IReadOnlyList<TestPluginDescriptor> result =
                this.pluginResolver!.ResolveAndOrder<ITestForwardInterface, TestPluginDescriptor>([plugin]);

            // Assert
            CollectionAssert.AreEqual(descriptors.ToList(), result.ToList(), "Forward context should preserve order");
        }

        [TestMethod]
        [TestCategory("ResolveAndOrder")]
        public void ResolveAndOrder_ReverseContext_ReturnsInReverseOrder()
        {
            TestPluginDescriptor[] descriptors =
            [
                CreateTestDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1.0.0"),
                CreateTestDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1.0.0")
            ];

            TestReversePlugin plugin = new(descriptors);

            IReadOnlyList<TestPluginDescriptor> result = this.pluginResolver!
                .ResolveAndOrder<ITestReverseInterface, TestPluginDescriptor>([plugin]);

            CollectionAssert.AreEqual(descriptors.Reverse().ToList(), result.ToList(), "Reverse context should invert order");
        }

        #endregion

        #region PluginResolverTests: Complex Scenarios

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_ValidDependencyIncludesBothPlugins()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")])
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(2, result.Count(), "Should include both plugins when dependency is satisfied");

            HashSet<Guid> returnedIds = [.. result.Select(d => d.PluginID)];

            Assert.IsTrue(returnedIds.Contains(plugin1Id), "Should contain Plugin1");
            Assert.IsTrue(returnedIds.Contains(plugin2Id), "Should contain Plugin2");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_MissingDependencyFiltersOutDependentPlugin()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid missingId = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();
            Guid missingInterfaceId = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(missingId, missingInterfaceId, "1.0.0", "2.0.0")])
            ];

            // Act
            IEnumerable<TestPluginDescriptor> result = this.pluginResolver!.ResolveDescriptors(descriptors);

            // Assert
            Assert.AreEqual(1, result.Count(), "Should filter out Plugin2 due to missing dependency");
            Assert.AreEqual(plugin1Id, result.First().PluginID, "Should only include Plugin1");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_DependencyWithoutLoadOrderDoesNotAffectSequence()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")]),
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0")
            ];

            // Act
            IList<TestPluginDescriptor> result = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];

            // Assert
            Assert.AreEqual(2, result.Count, "Should return both descriptors");

            HashSet<Guid> returnedIds = [.. result.Select(d => d.PluginID)];
            Assert.IsTrue(returnedIds.Contains(plugin1Id), "Should contain Plugin1");
            Assert.IsTrue(returnedIds.Contains(plugin2Id), "Should contain Plugin2");
        }

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_DependencyAndLoadOrderBothConstraintsApplied()
        {
            // Arrange
            Guid plugin1Id = Guid.NewGuid();
            Guid plugin2Id = Guid.NewGuid();
            Guid plugin3Id = Guid.NewGuid();
            Guid interface1Id = Guid.NewGuid();
            Guid interface2Id = Guid.NewGuid();
            Guid interface3Id = Guid.NewGuid();

            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor(plugin1Id, interface1Id, "1.0.0"),
                CreateTestDescriptor(plugin2Id, interface2Id, "1.0.0", dependsOn: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")],
                    loadAfter: [new PluginInterfaceReference(plugin1Id, interface1Id, "1.0.0", "2.0.0")]),
                CreateTestDescriptor(plugin3Id, interface3Id, "1.0.0")
            ];

            // Act
            List<TestPluginDescriptor> result = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];

            // Assert
            Assert.AreEqual(3, result.Count, "Should return all descriptors (dependency satisfied)");

            int index1 = result.ToList().FindIndex(d => d.PluginID == plugin1Id);
            int index2 = result.ToList().FindIndex(d => d.PluginID == plugin2Id);

            Assert.IsTrue(index1 < index2, "Plugin1 should load before Plugin2 (LoadAfter constraint)");
        }


        #endregion

        #region PluginResolverTests: Error Handling

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public void ResolveDescriptors_DeterministicOrderingReturnsSameOrderForSameInput()
        {
            // Arrange
            List<TestPluginDescriptor> descriptors =
            [
                CreateTestDescriptor("Plugin3", "1.0.0"),
                CreateTestDescriptor("Plugin1", "1.0.0"),
                CreateTestDescriptor("Plugin2", "1.0.0")
            ];

            // Act
            IList<TestPluginDescriptor> result1 = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];
            IList<TestPluginDescriptor> result2 = [.. this.pluginResolver!.ResolveDescriptors(descriptors)];

            // Assert
            Assert.AreEqual(result1.Count, result2.Count, "Results should have same count");
            for (int i = 0; i < result1.Count; i++)
            {
                Assert.AreEqual(result1[i].PluginID, result2[i].PluginID,
                    $"Plugin at index {i} should be the same in both results for deterministic ordering");
            }
        }

        #endregion


        private static TestPluginDescriptor CreateTestDescriptor(
            string pluginName,
            string version,
            IEnumerable<PluginInterfaceReference>? dependsOn = null,
            IEnumerable<PluginInterfaceReference>? conflictsWith = null,
            IEnumerable<PluginInterfaceReference>? loadBefore = null,
            IEnumerable<PluginInterfaceReference>? loadAfter = null)
        {
            return CreateTestDescriptor(
                Guid.Parse(pluginName.GetHashCode().ToString("X").PadLeft(32, '0')[..8] + "-0000-0000-0000-000000000000"),
                Guid.NewGuid(),
                version,
                dependsOn,
                conflictsWith,
                loadBefore,
                loadAfter);
        }

        private static TestPluginDescriptor CreateTestDescriptor(
            Guid pluginId,
            Guid descriptorId,
            string version,
            IEnumerable<PluginInterfaceReference>? dependsOn = null,
            IEnumerable<PluginInterfaceReference>? conflictsWith = null,
            IEnumerable<PluginInterfaceReference>? loadBefore = null,
            IEnumerable<PluginInterfaceReference>? loadAfter = null)
        {
            return new TestPluginDescriptor(
                PluginID: pluginId,
                InterfaceID: descriptorId,
                Version: version,
                LoadBefore: loadBefore,
                LoadAfter: loadAfter,
                DependsOn: dependsOn,
                ConflictsWith: conflictsWith);
        }
    }
}
