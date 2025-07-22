namespace PlugHub.Shared
{
    /// <summary>
    /// Represents a reference to another plugin interface, describing constraints or requirements for relationships such as dependencies, conflicts, or optional features.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PluginInterfaceReference"/> class with details identifying the target plugin interface, its version range, and other optional constraints or metadata.
    /// </remarks>
    /// <param name="pluginId">The unique identifier (GUID) of the referenced plugin.</param>
    /// <param name="interfaceId">The unique identifier (GUID) of the referenced plugin interface.</param>
    /// <param name="minVersion">Minimum acceptable version (inclusive) for the referenced interface.</param>
    /// <param name="maxVersion">Maximum acceptable version (inclusive) for the referenced interface.</param>
    public class PluginInterfaceReference(Guid pluginId, Guid interfaceId, string minVersion, string maxVersion)
    {
        /// <summary>
        /// Unique identifier (GUID) for the referenced plugin, matching its PluginID.
        /// </summary>
        public Guid PluginID { get; set; } = pluginId;

        /// <summary>
        /// Unique identifier (GUID) for the referenced plugin interface, matching its InterfaceID.
        /// </summary>
        public Guid InterfaceID { get; set; } = interfaceId;

        /// <summary>
        /// Minimum acceptable version (inclusive) for the referenced plugin.
        /// </summary>
        public string MinVersion { get; set; } = minVersion;

        /// <summary>
        /// Maximum acceptable version (inclusive) for the referenced plugin.
        /// </summary>
        public string MaxVersion { get; set; } = maxVersion;

        /// <summary>
        /// Compares two version strings in the format "major.minor.patch".
        /// Returns -1 if <paramref name="v1"/> is less than <paramref name="v2"/>,
        /// 1 if <paramref name="v1"/> is greater than <paramref name="v2"/>, or 0 if equal.
        /// </summary>
        /// <param name="v1">The first version string.</param>
        /// <param name="v2">The second version string.</param>
        /// <returns>An integer indicating the comparison result.</returns>
        private static int CompareVersions(string v1, string v2)
        {
            if (v1 == null && v2 == null)
            {
                return 0;
            }

            if (v1 == null)
            {
                return -1;
            }

            if (v2 == null)
            {
                return 1;
            }

            int[] parts1 = [.. v1.Split('.').Select(int.Parse)];
            int[] parts2 = [.. v2.Split('.').Select(int.Parse)];
            int maxLength = Math.Max(parts1.Length, parts2.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;
                if (p1 != p2)
                {
                    return p1.CompareTo(p2);
                }
            }
            return 0;
        }

        /// <summary>
        /// Checks if the given interface ID and version is within this reference's version range.
        /// </summary>
        /// <param name="pluginID">The unique identifier (GUID) of the plugin to check.</param>
        /// <param name="interfaceID">The unique identifier (GUID) of the interface to check.</param>
        /// <param name="version">The version of the interface to check.</param>
        /// <returns>
        /// <c>true</c> if the interface ID matches and the version is within the range; otherwise, <c>false</c>.
        /// </returns>
        public bool Matches(Guid pluginID, Guid interfaceID, string version)
        {
            if (pluginID != this.PluginID || interfaceID != this.InterfaceID)
            {
                return false;
            }

            return CompareVersions(version, this.MinVersion) >= 0 &&
                   CompareVersions(version, this.MaxVersion) <= 0;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// Two <see cref="PluginInterfaceReference"/> instances are equal if their
        /// <see cref="InterfaceID"/>, <see cref="MinVersion"/>, and <see cref="MaxVersion"/> are all equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>
        /// <c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
        {
            return obj is PluginInterfaceReference other &&
                   this.PluginID == other.PluginID &&
                   this.InterfaceID == other.InterfaceID &&
                   CompareVersions(this.MinVersion, other.MinVersion) == 0 &&
                   CompareVersions(this.MaxVersion, other.MaxVersion) == 0;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(this.PluginID, this.InterfaceID, this.MinVersion, this.MaxVersion);

        /// <summary>
        /// Determines whether two <see cref="PluginInterfaceReference"/> instances are equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if both instances are equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator ==(PluginInterfaceReference a, PluginInterfaceReference b)
            => a?.Equals(b) ?? b is null;

        /// <summary>
        /// Determines whether two <see cref="PluginInterfaceReference"/> instances are not equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if the instances are not equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(PluginInterfaceReference a, PluginInterfaceReference b)
            => !(a == b);

        /// <summary>
        /// Returns <c>true</c> if the interface's version is less than the minimum version of the range.
        /// </summary>
        /// <param name="range">The interface version range to compare against.</param>
        /// <param name="iface">A tuple containing the plugin's ID, interface's ID and version.</param>
        /// <returns>
        /// <c>true</c> if the interface's version is less than the minimum version; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator <(PluginInterfaceReference range, (Guid pluginId, Guid interfaceId, string version) iface)
            => range.PluginID == iface.pluginId &&
               range.InterfaceID == iface.interfaceId &&
               CompareVersions(iface.version, range.MinVersion) < 0;

        /// <summary>
        /// Returns <c>true</c> if the interface's version is greater than the maximum version of the range.
        /// </summary>
        /// <param name="range">The interface version range to compare against.</param>
        /// <param name="iface">A tuple containing the interface's ID and version.</param>
        /// <returns>
        /// <c>true</c> if the interface's version is greater than the maximum version; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator >(PluginInterfaceReference range, (Guid pluginId, Guid interfaceId, string version) iface)
            => range.PluginID == iface.pluginId &&
               range.InterfaceID == iface.interfaceId &&
               CompareVersions(iface.version, range.MaxVersion) > 0;
    }
}
