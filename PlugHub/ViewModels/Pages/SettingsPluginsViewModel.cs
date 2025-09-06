using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace PlugHub.ViewModels.Pages
{
    public class PluginInterfaceDescriptorData
    {
        public string? PluginName { get; set; }
        public PluginDescriptor? Descriptor { get; set; }
        public int? SortOrder { get; set; }
        public string? State { get; set; }
        public string? Message { get; set; }
    }

    public partial class SettingsPluginsViewModel : BaseViewModel
    {
        private readonly ILogger<SettingsPluginsViewModel> logger;
        private readonly IConfigService configService;
        private readonly IConfigAccessorFor<PluginManifest> pluginManifest;
        private readonly IPluginRegistrar pluginRegistrar;
        private readonly IPluginResolver pluginResolver;
        private readonly IPluginCache pluginCache;


        private bool isUpdating = false;
        private bool isPluginCheckboxUpdating = false;
        private readonly string originalManifestJson = string.Empty;


        [ObservableProperty]
        private ObservableCollection<PluginViewModel> originalPlugins = [];

        [ObservableProperty]
        private ObservableCollection<PluginViewModel> plugins = [];

        [ObservableProperty]
        private ObservableCollection<PluginViewModel> filteredPlugins = [];

        [ObservableProperty]
        private ObservableCollection<string> availableCategories = [];

        [ObservableProperty]
        private ObservableCollection<string> selectedCategories = [];

        [ObservableProperty]
        private string pluginSearchText = string.Empty;

        [ObservableProperty]
        private bool hasSearchOrCategorySelected = false;

        [ObservableProperty]
        private bool isPanelVisible = false;

        [ObservableProperty]
        private string selectedInterface = string.Empty;

        [ObservableProperty]
        private string descriptorSearchText = string.Empty;

        [ObservableProperty]
        private PluginInterfaceDescriptorData? selectedDescriptor = new()
        {
            Descriptor = new PluginInjectorDescriptor(Guid.Empty, Guid.Empty, "", typeof(object))
        };

        [ObservableProperty]
        private ObservableCollection<PluginInterfaceDescriptorData> pluginInterfaceDescriptorData = [];

        [ObservableProperty]
        private ObservableCollection<PluginInterfaceDescriptorData> filteredInterfaceDescriptorData = [];

        [ObservableProperty]
        private bool showRestartBanner = false;


        public SettingsPluginsViewModel(ILogger<SettingsPluginsViewModel> logger, IConfigService configService, IPluginCache pluginCache, IPluginRegistrar pluginRegistrar, IPluginResolver pluginResolver)
        {
            this.logger = logger;
            this.configService = configService;
            this.pluginManifest = configService.GetAccessor<PluginManifest>();
            this.pluginRegistrar = pluginRegistrar;
            this.pluginResolver = pluginResolver;
            this.pluginCache = pluginCache;

            this.pluginSearchText = string.Empty;

            PluginManifest manifest = this.pluginManifest.Get();

            this.originalManifestJson = JsonSerializer.Serialize(manifest);

            List<PluginLoadState> interfaceStates = manifest.InterfaceStates;

            this.GatherPlugins(pluginCache.Plugins, interfaceStates);
            this.GatherSnapshot();
            this.GatherCategories();
            this.ApplyPluginFilters();

            foreach (PluginViewModel plugin in this.Plugins)
                plugin.IsEnabledChanged += this.OnPluginEnabledChanged;
        }


        private void GatherPlugins(IEnumerable<PluginReference> pluginMetadatas, List<PluginLoadState> interfaceStates)
        {
            this.Plugins = [.. pluginMetadatas
                .Select(pr =>
                {
                    List<PluginLoadState> relevantStates = [.. interfaceStates
                        .Where(s =>
                            string.Equals(s.AssemblyName, pr.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(s.ClassName, pr.TypeName, StringComparison.OrdinalIgnoreCase))
                    ];
                    bool pluginEnabled = relevantStates.Any(s => s.Enabled);
                    PluginViewModel pluginVm = PluginViewModel.CreateFromPluginType(pr.Type, pluginEnabled);
                    pluginVm.ProvidedDescriptors = [.. relevantStates
                        .Select(ls =>
                        {
                            PluginInterface? iface = pr.Interfaces.FirstOrDefault(pi =>
                                string.Equals(pi.InterfaceName, ls.InterfaceName, StringComparison.OrdinalIgnoreCase));
                            string interfaceName = (iface?.InterfaceName ?? ls.InterfaceName).Split('.').Last();
                            PluginDescriptorViewModel pivm = new(interfaceName, iface?.InterfaceType, ls.Enabled, ls.System);
                            pivm.IsEnabledChanged += () => this.OnInterfaceEnabledChanged(pluginVm, pivm);
                            return pivm;
                        })
                        .OrderBy(pivm => pivm.Name)];

                        if (!pluginVm.ProvidedDescriptors.Any())
                        {
                            pluginVm.IsEnabled = false;
                        }
                        else
                        {
                            bool anyEnabled = pluginVm.ProvidedDescriptors.Any(i => i.IsEnabled);
                            bool allEnabled = pluginVm.ProvidedDescriptors.All(i => i.IsEnabled);

                            pluginVm.IsEnabled = allEnabled ? true : anyEnabled ? null : false;
                        }

                    return pluginVm;
                })
                .OrderBy(vm => vm.AssemblyName)
                .ThenBy(vm => vm.Name)
                .ToList()];
        }
        private void GatherSnapshot()
        {
            this.OriginalPlugins = [.. this.Plugins
                .Select(p => new PluginViewModel
                {
                    Name = p.Name,
                    IsEnabled = p.IsEnabled,
                    ProvidedDescriptors = new ObservableCollection<PluginDescriptorViewModel>(
                        p.ProvidedDescriptors?.Select(i => new PluginDescriptorViewModel(i.Name, i.Type, i.IsEnabled, i.IsSystem)) ?? []
                    )
                }
            )];
        }
        private void GatherCategories()
        {
            this.AvailableCategories = [.. this.Plugins
                    .SelectMany(p => p.Categories ?? [])
                    .Distinct()
                    .OrderBy(c => c)];
        }


        partial void OnPluginSearchTextChanged(string value)
        {
            this.UpdateHasSearchOrCategorySelected();
            this.ApplyPluginFilters();
        }
        partial void OnSelectedCategoriesChanged(ObservableCollection<string> value)
        {
            UpdateHasSearchOrCategorySelected();
            ApplyPluginFilters();
        }
        private void UpdateHasSearchOrCategorySelected() =>
            this.HasSearchOrCategorySelected = !string.IsNullOrEmpty(this.PluginSearchText) || (this.SelectedCategories?.Any() ?? false);
        public void ApplyPluginFilters()
        {
            string searchLower = (this.PluginSearchText ?? "").Trim().ToLowerInvariant();
            ObservableCollection<string> categories = this.SelectedCategories ?? [];

            IEnumerable<PluginViewModel> filtered = [.. this.Plugins.Where(p =>
                // Search filter
                (string.IsNullOrEmpty(searchLower) ||
                    (p.Name?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                    (p.Description?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                    (p.Author?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false))
                // Category filter
                && (!categories.Any() || p.Categories.Any(c => categories.Contains(c)))
            )];

            this.FilteredPlugins.Clear();
            foreach (PluginViewModel item in filtered)
                this.FilteredPlugins.Add(item);
        }
        public void ClearAllPluginFilters()
        {
            this.PluginSearchText = string.Empty;
            this.SelectedCategories.Clear();
            this.ApplyPluginFilters();
        }


        partial void OnDescriptorSearchTextChanged(string value)
        {
            ApplyDescriptorFilters();
        }
        public void ApplyDescriptorFilters()
        {
            string searchLower = (this.DescriptorSearchText ?? "").Trim().ToLowerInvariant();

            IEnumerable<PluginInterfaceDescriptorData> filtered = this.PluginInterfaceDescriptorData.Where(d =>
                string.IsNullOrEmpty(searchLower) ||
                (d.PluginName?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (d.State?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (d.Message?.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ?? false)
            );

            this.FilteredInterfaceDescriptorData.Clear();
            foreach (PluginInterfaceDescriptorData item in filtered)
                this.FilteredInterfaceDescriptorData.Add(item);
        }


        private void OnPluginEnabledChanged(PluginViewModel plugin)
        {
            if (this.isUpdating) return;
            if (this.isPluginCheckboxUpdating) return;
            if (!plugin.ProvidedDescriptors.Any()) return;

            this.isUpdating = true;
            this.isPluginCheckboxUpdating = true;

            bool hasDefault = plugin.ProvidedDescriptors.Any(i => i.IsSystem);
            bool isIndeterminate = plugin.IsEnabled == null;

            if (isIndeterminate == false)
            {
                foreach (PluginDescriptorViewModel ifaceVm in plugin.ProvidedDescriptors)
                    ifaceVm.IsEnabled = true;

                this.pluginRegistrar.SetAllEnabled(plugin.PluginID, enabled: true);

                plugin.IsEnabled = true;
            }
            else
            {
                foreach (PluginDescriptorViewModel ifaceVm in plugin.ProvidedDescriptors)
                {
                    ifaceVm.IsEnabled = ifaceVm.IsSystem;
                }

                this.pluginRegistrar.SetAllEnabled(plugin.PluginID, enabled: false);

                foreach (PluginDescriptorViewModel? defaultIface in plugin.ProvidedDescriptors.Where(i => i.IsSystem))
                {
                    Type interfaceType = plugin.PluginType.GetInterface(defaultIface.Name)
                        ?? throw new InvalidOperationException($"Interface {defaultIface.Name} not found on type");

                    this.pluginRegistrar.SetEnabled(plugin.PluginID, interfaceType, enabled: true);
                }

                plugin.IsEnabled = hasDefault ? null : false;
            }

            this.UpdateShowRestartBanner();
            this.isPluginCheckboxUpdating = false;
            this.isUpdating = false;
        }
        private void OnInterfaceEnabledChanged(PluginViewModel plugin, PluginDescriptorViewModel ifaceVm)
        {
            if (this.isUpdating) return;
            this.isUpdating = true;

            Type? interfaceType = plugin.PluginType
                .GetInterfaces()
                .FirstOrDefault(t => t.Name == ifaceVm.Name);

            if (interfaceType != null)
            {
                if (ifaceVm.IsEnabled)
                {
                    this.pluginRegistrar.SetEnabled(plugin.PluginID, interfaceType, enabled: true);
                }
                else
                {
                    this.pluginRegistrar.SetEnabled(plugin.PluginID, interfaceType, enabled: false);
                }
            }

            bool anyEnabled = plugin.ProvidedDescriptors.Any(i => i.IsEnabled);
            bool allEnabled = plugin.ProvidedDescriptors.All(i => i.IsEnabled);

            plugin.IsEnabled = allEnabled ? true : anyEnabled ? null : false;

            this.UpdateShowRestartBanner();
            this.isUpdating = false;
        }
        private void UpdateShowRestartBanner()
        {
            this.ShowRestartBanner =
                this.OriginalPlugins.Count != this.Plugins.Count ||
                this.Plugins.Zip(this.OriginalPlugins, (c, o) =>
                    !string.Equals(c.Name, o.Name, StringComparison.OrdinalIgnoreCase) || c.IsEnabled != o.IsEnabled || !(c.ProvidedDescriptors ?? [])
                        .OrderBy(i => i.Name)
                        .Zip(o.ProvidedDescriptors ?? [], (ci, oi) =>
                            string.Equals(ci.Name, oi.Name, StringComparison.OrdinalIgnoreCase) && ci.IsEnabled == oi.IsEnabled)
                        .All(equal => equal)
                ).Any(diff => diff);
        }


        [RelayCommand]
        private void OpenDescriptorInfo(PluginDescriptorViewModel ifaceVm)
        {
            if (ifaceVm.Name == this.SelectedInterface)
            {
                this.CloseDescriptorInfo();
                return;
            }
            if (ifaceVm.Type == null)
            {
                this.logger.LogWarning("PluginInterfaceViewModel.Type was null for interface '{InterfaceName}'", ifaceVm.Name);
                this.CloseDescriptorInfo();
                return;
            }

            this.IsPanelVisible = true;
            this.SelectedInterface = ifaceVm.Name;

            this.PopulatePluginInterfaceDescriptorData(ifaceVm);
        }

        [RelayCommand]
        private void CloseDescriptorInfo()
        {
            this.IsPanelVisible = false;
            this.SelectedInterface = string.Empty;
        }

        [RelayCommand]
        private void RevertPluginChanges()
        {
            if (string.IsNullOrEmpty(this.originalManifestJson))
                return;
            PluginManifest? restoredManifest = JsonSerializer.Deserialize<PluginManifest>(this.originalManifestJson);
            if (restoredManifest == null || restoredManifest.InterfaceStates == null)
                return;

            this.pluginRegistrar.SaveManifest(restoredManifest);
            this.UpdateViewModelsFromManifest(restoredManifest);

            this.isUpdating = true;

            foreach (PluginViewModel pluginVm in this.Plugins)
            {
                if (pluginVm.ProvidedDescriptors != null && pluginVm.ProvidedDescriptors.Any())
                {
                    bool allEnabled = pluginVm.ProvidedDescriptors.All(d => d.IsEnabled);
                    bool noneEnabled = pluginVm.ProvidedDescriptors.All(d => !d.IsEnabled);
                    bool? triState = allEnabled ? true : (noneEnabled ? false : null);

                    if (pluginVm.IsEnabled != triState)
                        pluginVm.IsEnabled = triState;
                }
                else
                {
                    if (pluginVm.IsEnabled != false)
                        pluginVm.IsEnabled = false;
                }
            }

            this.isUpdating = false;

            this.GatherSnapshot();
            this.GatherCategories();
            this.ApplyPluginFilters();
            this.ShowRestartBanner = false;
        }


        private void PopulatePluginInterfaceDescriptorData(PluginDescriptorViewModel ifaceVm)
        {
            ArgumentNullException.ThrowIfNull(ifaceVm);
            ArgumentNullException.ThrowIfNull(ifaceVm.Type);

            List<PluginLoadState> manifestStates = this.pluginManifest.Get().InterfaceStates;
            bool isOrdered = false;

            DescriptorProviderAttribute? providesDescAttr = ifaceVm.Type.GetCustomAttribute<DescriptorProviderAttribute>(inherit: false);
            if (providesDescAttr != null)
                isOrdered = providesDescAttr.DescriptorIsOrdered;

            IEnumerable<PluginDescriptor> descriptors = this.pluginRegistrar.GetDescriptorsForInterface(ifaceVm.Type);
            PluginResolutionContext<PluginDescriptor> context = this.pluginResolver.ResolveContext(descriptors);
            IReadOnlyList<PluginDescriptor> sortedDescriptors = context.GetSorted();

            Dictionary<PluginDescriptor, int> sortedIndexLookup = sortedDescriptors
                .Select((desc, index) => new { desc, index })
                .ToDictionary(x => x.desc, x => x.index);

            List<PluginInterfaceDescriptorData> rows = [];

            foreach (PluginDescriptor descriptor in descriptors)
            {
                string pluginName = "Unknown Plugin";
                PluginReference? pluginRef = this.pluginCache.Plugins.FirstOrDefault(p =>
                    p.Metadata.PluginID == descriptor.PluginID);
                if (pluginRef != null)
                    pluginName = pluginRef.Metadata.Name ?? "Unknown Plugin";

                int sortOrder = -1;
                string state;
                string message;

                PluginLoadState? manifestState = manifestStates.FirstOrDefault(s =>
                    s.PluginId == descriptor.PluginID &&
                    string.Equals(
                        s.InterfaceName.Split('.').Last(),
                        this.SelectedInterface,
                        StringComparison.OrdinalIgnoreCase));

                if (manifestState != null && !manifestState.Enabled)
                {
                    rows.Add(new PluginInterfaceDescriptorData
                    {
                        PluginName = pluginName,
                        Descriptor = descriptor,
                        SortOrder = null,
                        State = "❌",
                        Message = "Interface disabled"
                    });
                    continue;
                }

                if (context.DependencyDisabled.Contains(descriptor))
                {
                    state = "🚫";
                    sortOrder = -1;
                    message = "Missing dependency: \nPlease check logs for details!";
                }
                else if (context.ConflictDisabled.Contains(descriptor))
                {
                    state = "⚡";
                    sortOrder = -2;
                    IEnumerable<PluginInterfaceReference> conflicts = descriptor.ConflictsWith ?? [];
                    IEnumerable<string> conflictingNames = conflicts.Select(conf =>
                    {
                        PluginReference? plugin = this.pluginCache.Plugins.FirstOrDefault(p => p.Metadata.PluginID == conf.PluginID);
                        string pluginName = plugin?.Metadata?.Name ?? conf.PluginID.ToString();
                        return $"{pluginName}";
                    });
                    message = "Conflicts with: \n" +
                        (conflictingNames.Any()
                            ? string.Join(", ", conflictingNames)
                            : "Unknown plugin(s).");
                }
                else if (sortedIndexLookup.TryGetValue(descriptor, out int index))
                {
                    sortOrder = index;
                    if (isOrdered)
                    {
                        if (index == 0)
                        {
                            state = "⭐";
                            message = "Primary Descriptor.";
                        }
                        else
                        {
                            state = $"{index}";
                            message = $"Sort order: {index}";
                        }
                    }
                    else
                    {
                        state = "✅";
                        message = $"Sort order: {index}";
                    }
                }
                else
                {
                    state = "❓";
                    message = "Unresolved state: this plugin was neither sorted nor explicitly rejected.";
                }

                rows.Add(new PluginInterfaceDescriptorData
                {
                    PluginName = pluginName,
                    Descriptor = descriptor,
                    SortOrder = sortOrder,
                    State = state,
                    Message = message
                });
            }

            this.PluginInterfaceDescriptorData = [.. rows];
            this.ApplyDescriptorFilters();
        }
        private void UpdateViewModelsFromManifest(PluginManifest restoredManifest)
        {
            List<PluginLoadState> interfaceStates = restoredManifest.InterfaceStates;

            foreach (PluginViewModel pluginVm in this.Plugins)
            {
                List<PluginLoadState> statesForPlugin = [.. interfaceStates.Where(s =>
                    string.Equals(s.AssemblyName, pluginVm.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.ClassName, pluginVm.TypeName, StringComparison.OrdinalIgnoreCase))
                ];

                bool isEnabled = statesForPlugin.Any(s => s.Enabled);
                this.isUpdating = true;

                pluginVm.IsEnabled = isEnabled;

                foreach (PluginDescriptorViewModel descriptorVm in pluginVm.ProvidedDescriptors)
                {
                    PluginLoadState? state = statesForPlugin.FirstOrDefault(s =>
                        s.InterfaceName.Split('.').Last().Equals(descriptorVm.Name, StringComparison.OrdinalIgnoreCase));

                    descriptorVm.IsEnabled = state?.Enabled ?? false;
                }
                this.isUpdating = false;
            }
        }
    }
}