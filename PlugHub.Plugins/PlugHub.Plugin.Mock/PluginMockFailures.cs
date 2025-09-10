using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Models.Plugins;
using System.Collections;

namespace PlugHub.Plugin.Mock
{
    /// <summary>
    /// Demonstrates a plugin that declares a dependency on another plugin's service interface.
    /// This is used to test dependency resolution and loading order within the plugin system.
    /// </summary>
    /// <remarks>
    /// The dependency is expressed via the <see cref="DependsOn"/> collection, indicating that this plugin requires another plugin to be loaded first.
    /// If the dependency is missing or incompatible, this plugin should not load.
    /// </remarks>
    public class PluginMockDepend : PluginBase, IPluginDependencyInjection
    {
        #region PluginMockDepend: Key Fields

        public new static Guid PluginID => new("ed35d473-f898-4efb-85d1-0a5d2d03fc01");
        public new static string IconSource => "avares://PlugHub.Plugin.Mock/Assets/ic_fluent_link_24_filled.png";
        public new static string Name => "Plughub: Mock Depend";
        public new static string Description => "";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Depends",
            "Services",
        ];


        #endregion

        #region PluginMockDepend: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion

        #region PluginMockDepend: IPluginDependencyInjection

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f1ab6f62-74c2-4be2-af05-62e3fe54f349"),
                    Version: Version,
                    InterfaceType: typeof(IList),
                    ImplementationType: typeof(object),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [
                        new PluginInterfaceReference(
                            Guid.Parse("8557da82-b6fc-4be1-8731-e32e449285ab"),
                            Guid.Parse("d018bc99-6156-46e5-ae7c-cf1f2c0255f0"),
                            "0.0.1",
                            "1.0.0"
                        )
                    ]
                )
            ];
        }

        #endregion
    }

    /// <summary>
    /// Demonstrates a plugin that explicitly conflicts with another plugin’s implementation.
    /// </summary>
    /// <remarks>
    /// The conflict is declared in the <see cref="ConflictsWith"/> collection to prevent incompatible plugins from loading together.
    /// If conflicting plugins are detected, the system avoids loading this plugin to maintain stability.
    /// </remarks>
    public class PluginMockConflict : PluginBase, IPluginDependencyInjection
    {
        #region PluginMockConflict: Key Fields

        public new static Guid PluginID => Guid.Parse("026e0a9b-8b53-4e7f-837d-026dd60d8882");
        public new static string IconSource => "avares://PlugHub.Plugin.Mock/Assets/ic_fluent_link_dismiss_24_filled.png";
        public new static string Name => "Plughub: Mock Conflict";
        public new static string Description => "";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Conflicts",
            "Services",
        ];

        #endregion

        #region PluginMockConflict: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion

        #region PluginMockConflict: IPluginDependencyInjection

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("41c8c019-3b8e-436a-bfe2-5682913b0ce8"),
                    Version: Version,
                    InterfaceType: typeof(IList),
                    ImplementationType: typeof(object),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [
                        new PluginInterfaceReference(
                            Guid.Parse("ed35d473-f898-4efb-85d1-0a5d2d03fc01"),
                            Guid.Parse("f1ab6f62-74c2-4be2-af05-62e3fe54f349"),
                            "0.0.0",
                            "1.0.0"
                        )
                    ],
                    DependsOn: [])
            ];
        }

        #endregion
    }

    /// <summary>
    /// Represents an abstract plugin class that should never be loaded or instantiated as a plugin.
    /// </summary>
    /// <remarks>
    /// Abstract base classes are excluded from loading since they cannot provide concrete implementations or descriptors.
    /// This serves as an example of how non-instantiable plugins are handled by the system.
    /// </remarks>
    public abstract class PluginMockAbstract : PluginBase
    {
        #region PluginMockDepend: Key Fields

        public new static Guid PluginID => Guid.Parse("ae270d96-3f99-4b19-94ec-ecd61d2ecd83");
        public new static string IconSource => "";
        public new static string Name => "Plughub: Mock Abstract";
        public new static string Description => "This plugin should never load as it's an abstract class.";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Abstract",
            "Services",
        ];

        #endregion

        #region PluginMockDepend: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion
    }

    /// <summary>
    /// Plugin that implements no descriptor interfaces and thus provides no extension points or contributions.
    /// </summary>
    /// <remarks>
    /// Plugins without implemented descriptor interfaces are considered invalid or inert and should not be loaded by the system.
    /// This demonstrates the requirement that a plugin must expose at least one extension interface to be active.
    /// </remarks>
    public class PluginMockNoInterfaces : PluginBase
    {
        #region PluginMockNoInterfaces: Key Fields

        public new static Guid PluginID => Guid.Parse("45bc53be-bff0-4f46-ad13-d483004cd8c8");
        public new static string IconSource => "";
        public new static string Name => "Plughub: Mock No Interfaces";
        public new static string Description => "This plugin should never load as it implements no descriptor interfaces.";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Services",
        ];

        #endregion

        #region PluginMockNoInterfaces: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion
    }

    /// <summary>
    /// Demonstrates a plugin with a duplicate PluginID matching another plugin.
    /// </summary>
    /// <remarks>
    /// This plugin shares its PluginID with PluginMockNoInterfaces, which causes identity conflicts.
    /// The system requires each plugin to have a unique PluginID to avoid ambiguity during loading and management.
    /// </remarks>
    public class PluginMockDuplicateA : PluginBase, IPluginDependencyInjection
    {
        #region PluginMockDuplicate: Key Fields

        public new static Guid PluginID => Guid.Parse("45bc53be-bff0-4f46-ad13-d483004cd8c8");
        public new static string IconSource => "";
        public new static string Name => "Plughub: Mock Duplicate A";
        public new static string Description => "This plugin is a duplicate of no interfaces.";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Services",
        ];

        #endregion

        #region PluginMockDuplicate: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion

        #region PluginMockDuplicate: IPluginDependencyInjection

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("416c0a5a-1d0c-41f6-9703-be0263003278"),
                    Version: Version,
                    InterfaceType: typeof(IList),
                    ImplementationType: typeof(object),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                )
            ];
        }

        #endregion
    }

    /// <summary>
    /// Demonstrates a plugin with a duplicate PluginID matching PluginMockDuplicateA but with a unique descriptor.
    /// </summary>
    /// <remarks>
    /// Although this plugin has a unique descriptor, its PluginID duplicates PluginMockDuplicateA.
    /// Duplicate PluginIDs must be avoided for consistent plugin identification and conflict-free loading.
    /// </remarks>
    public class PluginMockDuplicateB : PluginBase, IPluginDependencyInjection
    {
        #region PluginMockDuplicate: Key Fields

        public new static Guid PluginID => Guid.Parse("45bc53be-bff0-4f46-ad13-d483004cd8c8");
        public new static string IconSource => "";
        public new static string Name => "Plughub: Mock Duplicate B";
        public new static string Description => "This plugin is a duplicate of PluginMockDuplicateA plugin with unique descriptor.";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Services",
        ];

        #endregion

        #region PluginMockDuplicate: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion

        #region PluginMockDuplicate: IPluginDependencyInjection

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f1ab6f62-74c2-4be2-af05-62e3fe54f349"),
                    Version: Version,
                    InterfaceType: typeof(IList),
                    ImplementationType: typeof(object),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                )
            ];
        }

        #endregion
    }

    /// <summary>
    /// Demonstrates a plugin with a unique PluginID but a duplicate DescriptorID matching PluginMockDuplicateB.
    /// </summary>
    /// <remarks>
    /// This plugin’s descriptor shares the same DescriptorID as PluginMockDuplicateB, causing conflicts in service registration.
    /// DescriptorIDs must be unique to ensure proper management of plugin services and extensions.
    /// </remarks>
    public class PluginMockDuplicateC : PluginBase, IPluginDependencyInjection
    {
        #region PluginMockDuplicate: Key Fields

        public new static Guid PluginID => Guid.Parse("018ed126-faf0-4298-91c8-97dca8ad597f");
        public new static string IconSource => "";
        public new static string Name => "Plughub: Mock Duplicate C";
        public new static string Description => "This plugin is a duplicate of PluginMockDuplicateB's descriptor.";
        public new static string Version => "0.0.1";
        public new static string Author => "Enterlucent";
        public new static List<string> Categories { get; } = [
            "Diagnostics",
            "Services",
        ];

        #endregion

        #region PluginMockDuplicate: Metadata

        public new static string DocsLink => "https://enterlucent.com";
        public new static string SupportLink => "https://enterlucent.com";
        public new static string SupportContact => "contact@enterlucent.com";
        public new static string ChangeLog => "https://enterlucent.com";

        #endregion

        #region PluginMockDuplicate: IPluginDependencyInjection

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f1ab6f62-74c2-4be2-af05-62e3fe54f349"),
                    Version: Version,
                    InterfaceType: typeof(IList),
                    ImplementationType: typeof(object),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                )
            ];
        }

        #endregion
    }
}
