# CONTRIBUTING

Thank you for your interest in PlugHub. With the support of contributors like you, we aim to make PlugHub the leading open, enterprise-grade plugin orchestrator for modular software development.

This document is designed to help you get oriented—whether you're looking for ways to contribute, need guidance on how to get started, or want to share your work with the community. We know it can be challenging to figure out where to begin, or frustrating if you're eager to help but unsure of the process. Our goal is to make contributing to PlugHub as clear and accessible as possible, whether you're new to open source or an experienced developer.

You'll find information here on how to discover tasks, contribute effectively, and collaborate with others. If you already know what you want to work on, you'll also find step-by-step instructions to help you get your contributions reviewed and merged into the project.

We're glad to have you here—let's build something great together!

## Table of Contents

- [Getting Started](#getting-started)
- [Evangelist](#evangelist)
- [Quality Controller](#quality-controller)
  - [How to Open a Bug Report](#how-to-open-a-bug-report)
- [Product Designer](#product-designer)
  - [When Is It a Feature Request?](#when-is-it-a-feature-request)
  - [How to Open a Feature Request](#how-to-open-a-feature-request)
  - [How to Open a Documentation Request](#how-to-open-a-documentation-request)
- [Technical Writer](#technical-writer)
  - [Types of Documentation We Need](#types-of-documentation-we-need)
  - [How to Find Documentation Tasks](#how-to-find-documentation-tasks)
  - [How to Submit Documentation](#how-to-submit-documentation)
- [Programmer](#programmer)
  - [How to Find Programming Tasks](#how-to-find-programming-tasks)
  - [How to Submit Code](#how-to-submit-code)
- [Attribution](#attribution)


## Getting Started
In every successful software project, tasks are divided into manageable components, each tackled by contributors with the passion and expertise to see them through. At PlugHub, these contributions span a variety of roles, each vital to the health and progress of the project:

-   [Evangelist](#evangelist) – Someone who deeply understands and believes in PlugHub, helping to connect and grow the community.
-   [Quality Controller](#quality-controller) – Someone who enjoys testing the limits of the system, identifying issues, and ensuring PlugHub remains robust and reliable.
-   [Product Designer](#product-designer) - Someone focuses on defining features, user experience, and overall product direction.
-   [Technical Writer](#technical-writer) – Someone who can clarify complex concepts and help others understand how to use or contribute to PlugHub.
-   [Programmer](#programmer) – Someone who implements new features, fixes bugs, and helps evolve the codebase.

While these are some of the primary roles, there are many ways to contribute—even if you're not a programmer or a technical writer. Documentation always benefits from new eyes and improvements, whether it's fixing typos, updating outdated information, or adding new tutorials and explanations. Reporting and investigating bugs, researching feature requests, and participating in discussions are all valuable contributions.

Many contributors wear multiple hats: a programmer might help with documentation, a technical writer might evangelize PlugHub at events, and a quality controller might propose new features. These roles are meant as a guide for organizing work, not as strict boundaries.

When contributing, please remember that we adhere to the [Contributor Covenant Code of Conduct](./CODE_OF_CONDUCT.md), and we expect all community members to do the same. Take a moment to review this document if you haven't already.


## Evangelist
This is by far the most open-ended of the roles—there's no telling who might discover PlugHub, become passionate about it, and want to share it with others. We welcome and encourage your enthusiasm for spreading the word about PlugHub! However, we ask that you keep a few important guidelines in mind:

### Prohibitions
- **No Lying** – Do not provide false or misleading information about PlugHub, its developers, its community, contributors (past, present, or future), or anyone or anything related to the project.
- **Post to Appropriate Places** – Only share information about PlugHub in venues where it is relevant and welcome.
- **Respect Others' Disinterest** – If a community or individual is not interested in PlugHub, please respect their wishes and do not persist or become a nuisance.
- **No Hype** – Avoid over-promising or creating unrealistic expectations. Progress happens at its own pace, and hype can lead to disappointment.

Outside of these points, feel free to share PlugHub's virtues and invite others to join our community. Let developers and users know they are welcome to contribute in any way they can. Ultimately, PlugHub is about people and our shared vision—not just code, documents, or assets.


## Quality Controller
These are the true warriors, fighting in the trenches to bring a better experience for the rest, it can not be stressed enough how important good Quality Controllers are to a project.  Many software houses tend to under value the work that Quality Controllers provide and often leave it to the community to debug (oh ...) their code, assets and documentation.  Here we understand the absolute need for the services they provide and are grateful for every detailed bug report provided.

### How to Open a Bug Report
1. Determine if the bug has already been [reported](https://github.com/enterlucent/plughub/issues?q=label%3Abug+is%3Aopen)
    * If you find that the Issue has already been opened then a new Issue is not needed.
    * Feel free to add your experiences to bug reports and to reopen bug Issues if it crops up again.
2. Create a "Bug Report" on the [Issues Tracker](https://github.com/enterlucent/plughub/issues/new?template=report-bug.yml).
    * Make sure that you are very detailed with what you are experiencing
    * If this is a documentation bug then add documentation to the labels
3. Check back often, or enable the notification options, in case others have questions or need further clarification


## Product Designer
Product Designers contribute to PlugHub by shaping how its features, workflows, and user experience come together. Much of what makes a great platform depends on how thoughtfully each component is designed and integrated. The hallmark of strong product design is when the experience feels seamless, intuitive, and empowers users to achieve their goals efficiently.

With the insight and creativity of dedicated Product Designers, PlugHub will continue to grow into a robust, user-friendly, and industry-leading plugin orchestrator. Your ideas and attention to detail help ensure that every aspect of PlugHub meets the needs of our diverse community.

### When Is It a Feature Request?
Distinguishing between a bug and a feature request can sometimes be nuanced. In general, a [Feature Request](https://github.com/enterlucent/plughub/issues/new?template=request-feature.md) is appropriate when you are proposing a change or addition to the current, expected behavior of PlugHub. Here are a few examples of what would constitute a feature request:

- An element or capability is completely missing from the project.
- Current behavior is missing a critical component needed for completeness.
- An existing element behaves as designed, but you believe its behavior is detrimental to the project and should be improved.

If you have an idea for the next great PlugHub feature, let us know by following the process below!

### How to Open a Feature Request
1. Check for Existing Requests:  
   Search [open feature requests](https://github.com/enterlucent/plughub/issues?q=label%3Aenhancement+is%3Aopen) to see if your idea has already been suggested.
    - If a similar request exists, expand on it by commenting rather than opening a duplicate.
2. Create a New Feature Request:  
   Open a [Feature Request](https://github.com/enterlucent/plughub/issues/new?template=request-feature.md) on the Issues Tracker.
    - Be as detailed as possible: describe what the feature should accomplish, how it might be implemented, and its impact on other features.
3. Engage in Discussion:  
   Check back often, or enable notifications, in case others have questions or feedback about your proposal.
4. Approval Process:  
   You'll know your feature is accepted when it receives the "approved" label.
    - If your feature depends on multiple requests, it will not be merged until all dependencies are complete.
    - Remember, even simple changes can have wide-ranging effects and must be fully explored before implementation.
    - Not all features can be added immediately, even if accepted; features are released in an orderly, maintainable way.

### How to Open a Documentation Request
1. Check for Existing Documentation Requests:  
   Review [open documentation requests](https://github.com/enterlucent/plughub/issues?q=label%3Adocumentation+is%3Aopen) to see if your suggestion has already been made.
    - Ask questions or discuss implementation details in the issue thread, or join our [Discord](https://discord.com/invite/mWDHDqkzeR) for real-time discussion.
    - If you find a similar request that's missing something critical, comment on it to expand the discussion.
2. Create a New Documentation Request:  
   Open a [Documentation Request](https://github.com/enterlucent/plughub/issues/new?template=request-documentation.md) explaining your proposed documentation changes or additions.
    - Be as detailed as possible: describe what the documentation should cover and what key areas need to be addressed.
3. Participate in the Review:  
   Others may ask questions or request changes, so check back or enable notifications.
4. Approval:  
   Your request is accepted when it receives the "approved" label.
    - If your request has multiple parts, it will not be merged until all are complete.


## Technical Writer
Technical Writers are the backbone of a well-organized project. PlugHub's success relies on clear, accessible, and up-to-date documentation so that contributors, users, and integrators can all stay on the same page.

Unlike traditional projects where documentation is often an afterthought, PlugHub treats documentation as a first-class priority. As PlugHub evolves through many iterations, the documentation must also adapt and grow. If you're excited by the challenge of keeping pace with a dynamic project—and helping others succeed—this role is for you!

### Types of Documentation We Need
- Code-Level Documentation  
  While programmers are responsible for documenting their code, technical writers can help clarify, expand, or improve code comments and API references when needed.
- Features and Functions  
  Document how PlugHub works, from broad overviews (e.g., “Getting Started with PlugHub”) to detailed guides (e.g., “Configuring Plugin Dependency Resolution”).
- Public Relations  
  Help shape how PlugHub is presented to the world—this includes website content, GitHub Pages, promotional materials, and press releases.
- Tutorials and How-To Guides  
  Create guides, articles, videos, or step-by-step instructions that help users and developers accomplish specific tasks or solve common problems.

### How to Find Documentation Tasks
1. Check the Issue Tracker:  
   Look for [Documentation Requests](https://github.com/enterlucent/plughub/issues?q=label%3Adocumentation,approved+is%3Aopen+no%3Aassignee) that are open, “approved,” and unassigned.  
   _Only begin work on issues that meet these criteria._
2. Review Existing Documentation:  
   There may be bugs or outdated sections in the current docs:
   - [Project Documentation](../docs/)
   - [GitHub Pages](https://enterlucent.github.io/plughub/)
   - [Project Wiki](https://github.com/enterlucent/plughub/wiki)
3. Submit Your Own Request:  
   If you spot missing or unclear documentation, submit a [Documentation Request](#how-to-open-a-documentation-request).

### How to Submit Documentation
1. [Find an Issue](#how-to-find-documentation-tasks) to resolve.
2. [Fork](https://help.github.com/articles/fork-a-repo/) the [PlugHub repository](https://github.com/enterlucent/plughub/).
3. [Create a new branch](https://help.github.com/articles/creating-and-deleting-branches-within-your-repository/):
   Use the format `issue#{issue-id}-{your-github-user}` (e.g., `issue#224-mdwigley`) for branches that resolve a single issue.  
   If your branch will resolve multiple issues, include each issue number separated by `#`, like so:  
   `issues#123#124#125-{your-github-user}` (e.g., `issues#123#124#125-mdwigley`).  
   This makes it clear which issues are being addressed by the branch and helps with tracking in the repository.
4. Make your documentation changes or additions on your new branch.
   - [Keep your fork in sync](https://help.github.com/articles/syncing-a-fork/) with the original repository.
   - If you need implementation help, request the "Help Wanted" label on the issue and ask for assistance on [Discord](https://discord.com/invite/mWDHDqkzeR).
5. When finished, submit a [Pull Request](https://help.github.com/articles/about-pull-requests/) containing only the relevant changes for the issue.
   - Be responsive to questions or requested changes during review.
6. Your Pull Request will be reviewed and, if approved, merged.
   - If your documentation is tied to a feature or asset issue, merging may wait until all related work is complete.
   - Once merged, you'll be recognized as an official contributor!


## Programmer
Programmers are responsible for implementing features proposed by Product Designers, maintaining code quality, and integrating assets and documentation contributed by the community.

### How to Find Programming Tasks
1. Browse the [Issue Tracker](https://github.com/enterlucent/plughub/issues?q=label%3Aenhancement,bug,approved+is%3Aopen+no%3Aassignee):
    - Look for open, “approved,” and unassigned issues labeled as `enhancement` or `bug`.
    - Ask questions in the Issue thread or join the [Discord](https://discord.com/invite/mWDHDqkzeR) server for real-time discussion.
    - **Only begin work on issues that are open, approved, and unassigned.**
2. Assist Team Members/Contributors already assigned to an Issue:
    - If you have expertise relevant to an assigned issue, offer your help.
    - The assigned contributor has final say on implementation details for their issue.
        - If you fundamentally disagree with an approach, voice your concerns in the Issue and consider submitting a future Feature Request to propose an alternative.
    - Not all issues or contributors will require assistance—don't take it personally if help isn't needed.
3. Submit your own [Feature Request](#how-to-open-a-feature-request):
    - If you have an idea for a new feature or improvement, follow the process for submitting a feature request.

### How to Submit Code

1. Set up your [Development Environment](../docs/Dev.Env.Win10.md).
2. [Find an issue](#how-to-find-programming-tasks) to resolve.
3. [Fork](https://help.github.com/articles/fork-a-repo/) the [PlugHub repository](https://github.com/enterlucent/plughub/).
4. [Create a new branch](https://help.github.com/articles/creating-and-deleting-branches-within-your-repository/)
   Use the format `issue#{issue-id}-{your-github-user}` (e.g., `issue#224-mdwigley`) for branches that resolve a single issue.  
   If your branch will resolve multiple issues, include each issue number separated by `#`, like so:  
   `issues#123#124#125-{your-github-user}` (e.g., `issues#123#124#125-mdwigley`).  
   This makes it clear which issues are being addressed by the branch and helps with tracking in the repository.
5. [Update submodules](https://gist.github.com/gitaarik/8735255#keeping-your-submodules-up-to-date) as needed.
6. Make your changes on your new branch:
    - [Keep your fork in sync](https://help.github.com/articles/syncing-a-fork/) with the main repository.
    - If you need implementation help, request the "Help Wanted" label on the issue and ask for assistance on [Discord](https://discord.com/invite/mWDHDqkzeR).
    - Ensure all files are whitespace-formatted (the default settings for Visual Studio or MonoDevelop are usually sufficient).
    - If using Visual Studio, apply the [official format settings](../docs/assets/files/CONTRIBUTING/.editorconfig).
    - Comment your code with both inline and construct-level documentation.
        - We use Doxygen for code-level documentation.
7. When finished, submit a [Pull Request](https://help.github.com/articles/about-pull-requests/) containing only the changes relevant to the issue.
    - Be responsive to questions or requested changes during review.
8. Your Pull Request will be reviewed and, if approved, merged.
    - If your code depends on related Asset or Documentation Issues, merging may wait until all dependencies are resolved.
    - Once merged, you'll be recognized as an official contributor!

### Commit Message Emoji Legend

We squash all commits before merging, so you’ll see a single, meaningful commit message for each pull request in the main branch. We use emojis in these messages to quickly convey the type of change. Use the table below as a legend for interpreting commit messages you see in the repository:

| Emoji | Purpose             | Example Commit Message              |
|-------|---------------------|-------------------------------------|
| ✨    | New feature         | ✨ Add user authentication           |
| 🐛    | Bug fix             | 🐛 Fix login redirect issue          |
| 🧼    | Code cleanup        | 🧼 Remove unused imports             |
| 🔥    | Remove code/files   | 🔥 Delete deprecated API endpoints   |
| 📝    | Documentation       | 📝 Update README with setup steps    |
| ♻️    | Refactor            | ♻️ Refactor payment processing logic |
| 🚀    | Performance         | 🚀 Improve query response time       |
| ✅    | Tests               | ✅ Add unit tests for user model     |

> **Note:** You do not need to use these emojis in your individual commits. They are used in the final, squashed commit messages to keep our history clear and easy to scan.

For more examples and inspiration, see [gitmoji.dev](https://www.gitmoji.dev/).


## Attribution
This CONTRIBUTING document was compiled with the help of [nayafia/contributing-template](https://github.com/nayafia/contributing-template/blob/master/CONTRIBUTING-template.md).
