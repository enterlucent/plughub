namespace PlugHub.Shared.Models.Plugins
{
    public class PluginLoadState(Guid pluginId = default, string assemblyName = "Unknown", string implementationName = "Unknown", string interfaceName = "Unknown", bool enabled = false, bool system = false, int loadOrder = int.MaxValue)
    {
        public PluginLoadState()
            : this(default, "Unknown", "Unknown", "Unknown", false, false, int.MaxValue) { }

        public Guid PluginId { get; set; } = pluginId;
        public string AssemblyName { get; set; } = assemblyName;
        public string ImplementationName { get; set; } = implementationName;
        public string InterfaceName { get; set; } = interfaceName;
        public bool System { get; set; } = system;
        public bool Enabled { get; set; } = enabled;
        public int LoadOrder { get; set; } = loadOrder;
    }
    public class PluginManifest
    {
        public List<PluginLoadState> InterfaceStates { get; set; } = [];
    }
}