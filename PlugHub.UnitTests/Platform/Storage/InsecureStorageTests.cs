using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Platform.Storage;
using PlugHub.Shared.Interfaces.Platform.Storage;

namespace PlugHub.UnitTests.Platform.Storage
{
    internal static class AsyncEnumerableHelpers
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> src)
        {
            List<T> list = new();
            await foreach (T? item in src) list.Add(item);
            return list;
        }
    }

    [TestClass]
    public sealed partial class InsecureStorageTests
    {
        private MSTestHelpers msTestHelpers = null!;
        private InsecureStorage storage = null!;
        private static readonly string[] expected = ["alpha1", "alpha2"];

        [TestInitialize]
        public void Setup()
        {
            this.msTestHelpers = new MSTestHelpers();

            this.storage = new InsecureStorage(new NullLogger<InsecureStorage>());
            this.storage.Initialize(this.msTestHelpers.TempDirectory, false, false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.storage.Dispose();
            this.msTestHelpers.Dispose();
        }

        #region InsecureStorageTests: KeyHandling

        [TestMethod]
        [TestCategory("KeyHandling")]
        public async Task SaveThenLoad_RoundTripsBytes()
        {
            string id = "roundtrip";
            byte[] sample = [1, 2, 3, 4, 5];

            await this.storage.SaveAsync(id, sample);
            byte[]? loaded = (await this.storage.TryLoadAsync(id))?.ToArray();

            CollectionAssert.AreEqual(sample, loaded, "Stored bytes should be retrieved intact.");
        }

        [TestMethod]
        [TestCategory("KeyHandling")]
        public async Task InvalidId_ThrowsArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await this.storage.SaveAsync("bad/id", new byte[1]));
        }

        #endregion

        #region InsecureStorageTests: Accessors

        [TestMethod]
        [TestCategory(nameof(Accessors))]
        public async Task TryLoadAsync_UnknownId_ReturnsNull()
        {
            ReadOnlyMemory<byte>? result = await this.storage.TryLoadAsync("does-not-exist");
            Assert.IsNull(result, "Unknown blob id should return null.");
        }

        #endregion

        #region InsecureStorageTests: Mutators

        [TestMethod]
        [TestCategory("Mutators")]
        public async Task SaveAsync_NewId_RaisesAddedEvent()
        {
            string id = "ev-added";
            byte[] data = [9];

            SecureStorageChangeKind? observed = null;
            this.storage.Changed += (_, e)
                => observed = e.Kind;

            await this.storage.SaveAsync(id, data);

            Assert.AreEqual(SecureStorageChangeKind.Added, observed, "Added event must be raised.");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public async Task SaveAsync_ExistingId_RaisesUpdatedEvent()
        {
            string id = "ev-updated";
            await this.storage.SaveAsync(id, new byte[] { 1 });

            SecureStorageChangeKind? observed = null;
            this.storage.Changed += (_, e) => observed = e.Kind;

            await this.storage.SaveAsync(id, new byte[] { 2 });

            Assert.AreEqual(SecureStorageChangeKind.Updated, observed, "Updated event must be raised.");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public async Task DeleteAsync_RemovesBlobAndRaisesEvent()
        {
            string id = "ev-deleted";
            await this.storage.SaveAsync(id, new byte[] { 3 });

            SecureStorageChangeKind? observed = null;
            this.storage.Changed += (_, e) => observed = e.Kind;

            await this.storage.DeleteAsync(id);

            Assert.AreEqual(SecureStorageChangeKind.Deleted, observed, "Deleted event must be raised.");
            Assert.IsNull(await this.storage.TryLoadAsync(id), "Blob should be gone.");
        }

        #endregion

        #region InsecureStorageTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task Blob_PersistsAcrossInstances()
        {
            string id = "persist";
            byte[] buf = [7, 7, 7];
            await this.storage.SaveAsync(id, buf);

            this.storage.Dispose();
            this.storage = new InsecureStorage(new NullLogger<InsecureStorage>());
            this.storage.Initialize(this.msTestHelpers.TempDirectory);

            byte[]? loaded = (await this.storage.TryLoadAsync(id))?.ToArray();
            CollectionAssert.AreEqual(buf, loaded, "Second instance should read persisted blob.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task ListIdsAsync_ReturnsExpectedIdsWithPrefix()
        {
            await this.storage.SaveAsync("alpha1", new byte[] { 1 });
            await this.storage.SaveAsync("alpha2", new byte[] { 2 });
            await this.storage.SaveAsync("beta1", new byte[] { 3 });

            List<string> ids = await this.storage.ListIdsAsync("alpha").ToListAsync();

            CollectionAssert.AreEquivalent(expected, ids);
        }

        #endregion

        #region InsecureStorageTests: Disposal

        [TestMethod]
        [TestCategory("Disposal")]
        public async Task OperationsAfterDispose_ThrowObjectDisposedException()
        {
            this.storage.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                async () => await this.storage.SaveAsync("x", new byte[1]));
        }

        #endregion

        #region InsecureStorageTests: Security

        // Currently no crypto logic in the store itself – left as a placeholder

        #endregion
    }
}
