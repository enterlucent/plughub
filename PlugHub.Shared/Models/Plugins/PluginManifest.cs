namespace PlugHub.Shared.Models.Plugins
{
    public class PluginLoadState(Guid pluginId = default, string assemblyName = "Unknown", string interfaceName = "Unknown", string className = "Unknown", bool enabled = false, bool system = false, int loadOrder = int.MaxValue)
    {
        public PluginLoadState()
            : this(default, "Unknown", "Unknown", "Unknown", false, false, int.MaxValue) { }

        public Guid PluginId { get; set; } = pluginId;
        public string AssemblyName { get; set; } = assemblyName;
        public string InterfaceName { get; set; } = interfaceName;
        public string ClassName { get; set; } = className;
        public bool System { get; set; } = system;
        public int LoadOrder { get; set; } = loadOrder;
        public bool Enabled { get; set; } = enabled;
    }
    public class PluginManifest
    {
        public List<PluginLoadState> DescriptorStates { get; set; } = [];
    }
}