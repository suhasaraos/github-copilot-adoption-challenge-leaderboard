---
Title: Local Agent
ActivityId: 12
---

### Summary

In this exercise you will explore the three built-in local agent modes in GitHub Copilot Chat: **Agent**, **Plan**, and **Ask**. Each mode is designed for a different workflow, from autonomous multi-step coding to codebase exploration. Expect to spend twenty to thirty minutes.

### What you will learn

- Switching between Agent, Plan, and Ask modes in Copilot Chat.
- Using Agent mode for autonomous multi-file edits with inline diffs.
- Using Plan mode to generate an implementation plan before writing code.
- Using Ask mode to answer codebase questions without making changes.
- Reviewing checkpoints and undoing agent actions.

### Before you start

Ensure VS Code has the latest GitHub Copilot Chat extension. Enable agent mode in settings (`chat.agent.enabled`). Open a repository with enough code to make multi-file tasks meaningful.

### Steps

- **Step 1.** Open Copilot Chat and use the mode picker to switch to **Ask** mode. Enter: `Explain how authentication is handled in this project.` Review the response, which references specific files without making edits.

- **Step 2.** Switch to **Plan** mode. Enter: `Add input validation to all API controller methods.` Copilot generates a step-by-step plan listing files to change and what to modify.

- **Step 3.** Review the generated plan. Edit or reorder steps if needed. When satisfied, click **Start Implementation** to hand off to Agent mode.

- **Step 4.** Observe Agent mode executing each step. Inline diffs appear in the editor as files are modified. A checkpoint is created after each step so you can undo individual changes.

- **Step 5.** Open the Timeline view or the undo history to inspect checkpoints. Roll back one change to see how Copilot restores the previous state.

- **Step 6.** Start a fresh Agent mode session and enter: `Write unit tests for the user service module.` Watch the agent create test files, add imports, and run tests autonomously.

- **Step 7.** Review all changes in the Source Control panel and commit them.

### Checkpoint

1. Did you successfully use Ask mode to get a codebase explanation without any file edits

- [ ] Yes
- [ ] No

2. Did Plan mode generate a clear implementation plan that you could review before execution

- [ ] Yes
- [ ] No

3. Did Agent mode apply inline diffs and create checkpoints you could inspect and undo

- [ ] Yes
- [ ] No

### Explore more

- [Local agents in VS Code](https://code.visualstudio.com/docs/copilot/agents/local-agents)

- [Copilot Chat agent mode tutorial](https://code.visualstudio.com/docs/copilot/chat/chat-agent-mode)

