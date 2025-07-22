using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services;
using PlugHub.Shared;

namespace PlugHub.UnitTests.Services
{
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
        public void ResolveDescriptors_EmptyCollection_ReturnsEmpty()
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
        public void ResolveDescriptors_SingleDescriptor_ReturnsSingle()
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
        public void ResolveDescriptors_MultipleIndependentDescriptors_ReturnsAll()
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

        #endregion

        #region PluginResolverTests: Dependency Resolution

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_MissingDependency_FiltersOutInvalidDescriptor()
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
        public void ResolveDescriptors_VersionMismatch_FiltersOutIncompatibleDescriptor()
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
        public void ResolveDescriptors_ConflictingDescriptors_FiltersOutConflicted()
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
        public void ResolveDescriptors_LoadBeforeConstraint_RespectsOrdering()
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
        public void ResolveDescriptors_LoadAfterConstraint_RespectsOrdering()
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

        #region PluginResolverTests: Complex Scenarios

        [TestMethod]
        [TestCategory("ResolveDescriptors")]
        public void ResolveDescriptors_ValidDependency_IncludesBothPlugins()
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
        public void ResolveDescriptors_MissingDependency_FiltersOutDependentPlugin()
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
        public void ResolveDescriptors_DependencyWithoutLoadOrder_DoesNotAffectSequence()
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
        public void ResolveDescriptors_DependencyAndLoadOrder_BothConstraintsApplied()
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
        public void ResolveDescriptors_DeterministicOrdering_ReturnsSameOrderForSameInput()
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
            Guid interfaceId,
            string version,
            IEnumerable<PluginInterfaceReference>? dependsOn = null,
            IEnumerable<PluginInterfaceReference>? conflictsWith = null,
            IEnumerable<PluginInterfaceReference>? loadBefore = null,
            IEnumerable<PluginInterfaceReference>? loadAfter = null)
        {
            return new TestPluginDescriptor(
                PluginID: pluginId,
                InterfaceID: interfaceId,
                Version: version,
                LoadBefore: loadBefore,
                LoadAfter: loadAfter,
                DependsOn: dependsOn,
                ConflictsWith: conflictsWith);
        }
    }
}
