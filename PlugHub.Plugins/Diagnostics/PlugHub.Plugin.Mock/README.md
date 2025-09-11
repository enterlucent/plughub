# PlugHub.Plugin.Mock

This is a **test plugin** for the PlugHub platform.  
PlugHub.Plugin.Mock is intended for demonstration and validation of the PlugHub plugin system.  
It serves as a reference implementation and is **not intended for production use**.

---

## About

PlugHub.Plugin.Mock demonstrates core plugin integration patterns for the PlugHub host application.  
It is included in the official PlugHub repository to showcase plugin structure, registration, and behavior within the modular PlugHub ecosystem.

### What does it demonstrate?
- **Type-safe configuration**: Example strongly-typed config model (`PluginMockConfig`) with permissions managed via access tokens.
- **Dependency injection**: Provides and consumes services (`IEchoService`) to illustrate cross-plugin communication and handler extension.
- **Token-based configuration security**: Shows owner/read/write access control on plugin configuration data.
- **Application rebranding**: Demonstrates white-label support by renaming PlugHub and overriding app storage directories when loaded at the system level.
- **Style and theming**: Supplies UI resources (e.g., icon styles) for host customization.
- **Extensible UI**: Registers navigation pages and configuration settings pages, giving end users familiar, integrated plugin experiences.

### Who should use this?

- **Plugin developers** wanting a working reference for descriptor interfaces and integration points.
- **Testers** or system integrators validating PlugHub-side changes to the plugin load, conflict, or configuration systems.

---

## License

This plugin is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.  
See the [LICENSE](LICENSE) file in this directory for full details.

---

For more information about PlugHub, plugin development, and community discussions, please refer to the main [PlugHub repository](../../) and documentation.
