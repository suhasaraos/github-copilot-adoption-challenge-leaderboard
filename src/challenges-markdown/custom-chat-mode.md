---
Title: Custom Agent Mode
ActivityId: 12
---

### Summary

In this advanced exercise, you will create a **.agent.md** custom agent called *CI/CD Release Manager* that instructs GitHub Copilot to assist with release pipelines, tagging, and changelog generation. Estimated time: thirty minutes.

### What you will learn

- Creating a `.agent.md` file to define a custom Copilot agent.
- Guiding Copilot's responses to focus on CI/CD workflows.
- Integrating and selecting a custom agent in VS Code Copilot Chat.
- Applying persona-driven suggestions to automate release processes.

### Before you start

Ensure you have the latest GitHub Copilot Chat extension in VS Code, and a repository containing at least one GitHub Actions workflow for CI/CD.

### Steps

- **Step 1.** Create a folder `.github/agents` in your repo.
- **Step 2.** Add a file `ci-cd-release-manager.agent.md` with agent instructions, limiting scope to CI/CD YAML edits, semantic versioning, and changelog automation. Refer to the [Custom Agent documentation](https://docs.github.com/en/copilot/concepts/agents/coding-agent/about-custom-agents#agent-profile-format) for guidance on structuring the agent file.
- **Step 3.** **Configure Custom Agents** from the agents dropdown to discover the new agent.
- **Step 4.** In Copilot Chat, select the **CI/CD Release Manager** agent from the agents dropdown.
- **Step 5.** Prompt: `Prepare a GitHub Actions workflow to handle semantic version bumping based on commit messages and auto-generate a changelog.`
- **Step 6.** Review, refine, and commit the generated workflow.

### Checkpoint

1. Was the custom agent selected and activated successfully?

- [ ] Yes
- [ ] No

2. Did Copilot suggest a workflow following persona guidelines?

- [ ] Yes
- [ ] No

### Explore more

- [Custom agents](https://docs.github.com/en/copilot/concepts/agents/coding-agent/about-custom-agents#agent-profile-format)
- [GitHub Actions documentation](https://docs.github.com/en/actions)
