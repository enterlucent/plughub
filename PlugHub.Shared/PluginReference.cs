namespace PlugHub.Shared
{
    /// <summary>
    /// Represents a reference to another plugin, describing constraints or requirements for relationships such as dependencies, conflicts, or optional features.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PluginReference"/> class with details identifying the target plugin, version range, and other optional constraints or metadata.
    /// </remarks>
    /// <param name="pluginID">The unique identifier of the referenced plugin.</param>
    /// <param name="minVersion">The minimum compatible version of the referenced plugin.</param>
    /// <param name="maxVersion">The maximum compatible version of the referenced plugin.</param>
    public class PluginReference(Guid pluginID, string minVersion, string maxVersion)
    {
        /// <summary>
        /// Unique identifier for the plugin. This ID should be unique across all plugins.
        /// This ID should match the <see cref="PluginBase.PluginID"/> of the plugin being referenced.
        /// </summary>
        public Guid PluginID { get; set; } = pluginID;

        /// <summary>
        /// Minimum version of the plugin.
        /// </summary>
        public string MinVersion { get; set; } = minVersion;

        /// <summary>
        /// Maximum version of the plugin.
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
        /// Checks if the given plugin ID and version is within this reference's version range.
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin to check.</param>
        /// <param name="version">The version of the plugin to check.</param>
        /// <returns>
        /// <c>true</c> if the plugin ID matches and the version is within the range; otherwise, <c>false</c>.
        /// </returns>
        public bool Matches(Guid pluginId, string version)
        {
            if (pluginId != this.PluginID)
            {
                return false;
            }

            return CompareVersions(version, this.MinVersion) >= 0 &&
                   CompareVersions(version, this.MaxVersion) <= 0;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// Two <see cref="PluginReference"/> instances are equal if their
        /// <see cref="PluginID"/>, <see cref="MinVersion"/>, and <see cref="MaxVersion"/> are all equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>
        /// <c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
        {
            return obj is PluginReference other &&
                   this.PluginID == other.PluginID &&
                   CompareVersions(this.MinVersion, other.MinVersion) == 0 &&
                   CompareVersions(this.MaxVersion, other.MaxVersion) == 0;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(this.PluginID, this.MinVersion, this.MaxVersion);

        /// <summary>
        /// Determines whether two <see cref="PluginReference"/> instances are equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if both instances are equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator ==(PluginReference a, PluginReference b)
            => a?.Equals(b) ?? b is null;

        /// <summary>
        /// Determines whether two <see cref="PluginReference"/> instances are not equal.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if the instances are not equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(PluginReference a, PluginReference b)
            => !(a == b);

        /// <summary>
        /// Returns <c>true</c> if the plugin's version is less than the minimum version of the range.
        /// </summary>
        /// <param name="range">The plugin version range to compare against.</param>
        /// <param name="plugin">A tuple containing the plugin's ID and version.</param>
        /// <returns>
        /// <c>true</c> if the plugin's version is less than the minimum version; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator <(PluginReference range, (Guid pluginId, string version) plugin)
            => range.PluginID == plugin.pluginId &&
               CompareVersions(plugin.version, range.MinVersion) < 0;

        /// <summary>
        /// Returns <c>true</c> if the plugin's version is greater than the maximum version of the range.
        /// </summary>
        /// <param name="range">The plugin version range to compare against.</param>
        /// <param name="plugin">A tuple containing the plugin's ID and version.</param>
        /// <returns>
        /// <c>true</c> if the plugin's version is greater than the maximum version; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator >(PluginReference range, (Guid pluginId, string version) plugin)
            => range.PluginID == plugin.pluginId &&
               CompareVersions(plugin.version, range.MaxVersion) > 0;
    }
}
