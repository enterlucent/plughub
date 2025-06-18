# PlugHub

[![License: LGPL](https://img.shields.io/badge/License-LGPL-blue.svg)](LICENSE.md) 
[![Join us on Discord](https://img.shields.io/badge/Discord-Join-blue?logo=discord)](https://discord.com/invite/mWDHDqkzeR) 
[![Release](https://img.shields.io/github/v/release/enterlucent/plughub?include_prereleases)](../../releases)

**PlugHub** is an enterprise-grade application designed around a modular plugin architecture, enabling developers and organizations to build, manage, and deploy extensible software systems with ease. It provides a robust framework where plugins serve as independent, interchangeable components that seamlessly integrate into a unified host application.

With PlugHub, you can rapidly extend functionality, validate plugin compatibility, and orchestrate complex workflows—all while maintaining a clean separation of concerns and maximizing maintainability.

**Join us** in creating a flexible and scalable platform that empowers innovation through modular design and dynamic extensibility.

## Table of Contents
- [Quick Start](#quick-start)
- [Getting Started](#getting-started)
- [Contributing](#contributing)
- [Authors](#authors)
- [License](#license)

## Quick Start
1. **Install PlugHub**  
   Download and install PlugHub from the [Releases page](../../releases).
2. **Run PlugHub**  
   Launch the application.
3. **Add Plugins**  
   Install plugins as needed to extend functionality.

## Getting Started
- [Getting Information](#how-to-get-information) – Where to find documentation and support resources.
- [Support Questions](#how-to-open-a-support-question) – Check if your question has already been answered.
- [Bug Report](#how-to-submit-a-bug-report) – See if your issue has already been reported.
- [Feature Requests](#how-to-request-a-feature) – Find out if your idea has already been suggested.
- [Site Mention](#how-to-request-a-site-mention) – Submit community resources for inclusion.

### How to Get Information
- **Documentation** – Comprehensive guides and references for PlugHub:
    - [Project Docs](docs/)
    - [GitHub Pages](https://enterlucent.github.io/plughub/)
    - [Wiki](../../wiki)
    - [Discussions](../../discussions)
- **Community Chat** – Join our [Discord](https://discord.com/invite/mWDHDqkzeR) for real-time support and collaboration.

## How to Open a Support Question
1. **Search Existing Discussions**  
   Browse [existing discussions](../../discussions/categories/support) to see if your question has already been addressed.
   - If you find a relevant discussion, join the conversation by commenting or reacting.
2. **Start a New Discussion**  
   If your question isn't answered, create a new discussion in the appropriate category (such as Q&A or Support).
   - Use a clear, descriptive title (e.g., “How do I configure plugins?”).
   - Provide detailed information and context to help others assist you efficiently.
3. **Follow Up**  
   Enable notifications or check back for responses. Please be patient, as maintainers and community members may reply asynchronously.

> Please use the Support category for these requests, not GitHub Issues. This helps keep actionable issues separate from community and promotional content, and makes it easier for maintainers to review and organize submissions.

### How to Submit a Bug Report
1. **Search for Existing Bugs:**  
   Review [open bug reports](../../issues?q=label%3Abug+is%3Aopen).
   - Add details or reopen if your issue matches an existing report.
2. **File a Bug Report:**  
   Use the [Bug Report template](../../issues/new?template=report-bug.yml).
   - Include detailed steps to reproduce, expected and actual behavior, and environment details.
3. **Monitor and Respond:**  
   Watch for follow-up questions or requests for additional information.

### How to Request a Feature
1. **Check for Existing Requests:**  
   Search [open feature requests](../../issues?q=label%3Aenhancement+is%3Aopen).
   - Comment to expand on existing requests; avoid duplicates.
2. **Submit a Feature Request:**  
   Open a [Feature Request](../../issues/new?template=request-feature.md).
   - Reference any related documentation needs in your request.
   - Clearly describe the feature, its purpose, and potential impact.
3. **Stay Engaged:**  
   Enable notifications or check back for discussion and status updates.
   - Features are accepted when labeled "approved" and may be scheduled for future releases.

## How to Request a Site Mention

If you have created a resource relevant to PlugHub and would like it featured on our GitHub Pages:

- Start a new discussion in the [General category](../../discussions/categories/general).
- Prefix your discussion title with [PROMO]** (e.g., `[PROMO] mysite.com: PlugHub Plugin Showcase`).
- Respond to any follow-up questions from maintainers.
- You will be notified if your submission is accepted and published.

> Please use the General category for these requests, not GitHub Issues. This helps keep actionable issues separate from community and promotional content, and makes it easier for maintainers to review and organize submissions.

## Contributing
Please review our [CONTRIBUTING](.github/CONTRIBUTING.md) guidelines for details on how to participate, coding standards, and the development workflow.  

Refer to our [Design Document](docs/Design.md) and [Code of Conduct](.github/CODE_OF_CONDUCT.md) for further information.

## Authors

* **Michael Wigley** - *Programming* - [mdwigley](https://github.com/mdwigley)

See the list of [contributors](../../graphs/contributors) who have participated in this project.

## License

This repository uses **dual licensing**:

### **Host Application & Shared Libraries**
- **LGPL-3.0** ([View License](LICENSE.md))
  - Applies to all code **except** official plugins.
  - Permits use in proprietary software.
  - Modifications to LGPL code must be shared under LGPL.

### **Official Plugins**
- **GPL-3.0** (see plugin-specific `LICENSE` files)
  - Applies to all code in `PlugHub.Plugins\PlugHub.Plugin.*` directories.
  - Plugins may be used freely.
  - **Derivative works** (forks, wrappers, modifications) must also be GPL-licensed.

> If a subfolder contains its own `LICENSE` file, that license applies to the code in that subfolder and takes precedence over the root license.  
> Always check the `LICENSE` file in each plugin directory for the specific terms that apply to that plugin.

| Component          | License | Key Requirement                          |
|--------------------|---------|------------------------------------------|
| Host/Shared Code   | LGPL    | Modifications must be shared as LGPL.    |
| Official Plugins   | GPL     | Derivatives must be GPL.                 |

