---
Title: Context augmented agent
ActivityId: 12
---

### Summary

In this advanced workflow, you'll configure a **GitHub Copilot Space** to centralize your project context so that Copilot can reference your organization's internal documentation and code. You'll then ask Copilot to draft an ADR proposing a migration from a monolith to microservices. This task takes about thirty minutes.

### What you will learn

- Creating and configuring a GitHub Copilot Space.

- Adding code, documents, and custom instructions to a Space.

- Sharing a Space across your organization to scale expertise.

- Prompting Copilot in a Space to draft an ADR using contextual knowledge.

### Before you start

Copilot Spaces are available to all GitHub Copilot users at [github.com/copilot/spaces](https://github.com/copilot/spaces). If you are on a **Copilot Business** or **Copilot Enterprise** plan, your organization admin must opt in to Copilot preview features. Prepare your documentation (Markdown, text, or supported formats) and ensure it is stored in a GitHub repository so it can be attached directly to the Space.

### Steps

- **Step 1.** Navigate to [github.com/copilot/spaces](https://github.com/copilot/spaces) and click **New space**. If creating a shared space for your organization, select your organization as the owner.

- **Step 2.** Give your Space a name (e.g., `Architecture Docs`) and add context sources. Click **Add context**, then select **Repository** to attach your architecture documentation repository directly from GitHub.

- **Step 3.** Add any additional files, notes, or specs relevant to your system's design by clicking **Add context** and choosing the appropriate source type.

- **Step 4.** Optionally, define **Custom instructions** within the Space to further tailor Copilot's responses — for example, specifying the ADR format your team follows.

- **Step 5.** Create a new branch with `git checkout -b adr-microservices`. Then open the Space chat and type:
`Draft an Architecture Decision Record for migrating our monolith to microservices using our current architecture documents as context.`

- **Step 6.** Review the generated ADR, which should include: *Context*, *Decision*, *Status*, and *Consequences*.

- **Step 7.** Save the draft to `docs/adr/0005-microservices-migration.md`, make any organization-specific edits, commit the file, and push the branch for review.

### Checkpoint

1. Did you successfully create a Copilot Space and add your documentation as context?

- [ ] Yes
- [ ] No

2. Did Copilot cite information from your documentation in the ADR draft?

- [ ] Yes
- [ ] No

3. Did you commit and push the ADR for review?

- [ ] Yes
- [ ] No

### Explore more

- [Copilot Spaces](https://docs.github.com/copilot/using-github-copilot/copilot-spaces/about-organizing-and-sharing-context-with-copilot-spaces)

- [Markdown ADR template](https://adr.github.io/madr/)
