---
Title: Subagents
ActivityId: 12
---

### Summary

In this hands-on exercise you will delegate complex, multi-step tasks to **subagents** in GitHub Copilot. A subagent is an isolated agent session that the primary agent spawns to perform focused research or implementation work, then returns a summary. Expect to spend twenty to thirty minutes.

### What you will learn

- Understanding how subagents provide isolated context for subtasks.
- Prompting the primary/default agent so it delegates work to a subagent automatically.
- Observing parallel subagent execution in the Copilot Chat panel.
- Using a custom agent definition to act as a reusable subagent.

### Before you start

Ensure you are running VS Code with the latest GitHub Copilot Chat extension. Agent mode must be enabled in settings (`chat.agent.enabled`). Open a repository that contains multiple files or modules so the agent has enough complexity to benefit from delegation.

### Steps

- **Step 1.** Open a Local Copilot Chat session and switch to **Agent** mode using the mode picker at the bottom of the panel.

- **Step 2.** Enter a prompt that naturally requires delegation, for example: `Review the entire codebase and perform an extensive research across all the public API endpoints in this project in parallel and then create a Markdown summary for each API endpoint that provides the following details - HTTP method, route, purpose, and enhancements.`

  **NOTE:** VS Code automatically (optionally) invokes built-in subagents depending on the task. If you don't see the subagents being automatically, you can try adding this to the prompt `Leverage subagents if needed`. **Tip** - Retry the task with a complex task involving the entire repo if this still doesn't work!

- **Step 3.** Watch the Chat panel. The agent should spawn a subagent to research the codebase. You will see a collapsed **Subagent** node appear with its own tool calls and progress indicator.

- **Step 4.** Expand the subagent node to inspect what tools it called and the summary it returned to the primary agent.

- **Step 5.** Now create a custom agent file at `.github/agents/researcher.md` with the following content: a description instructing it to search the codebase and return concise findings, and an `agents` front-matter field to allow it to be invoked as a subagent.

- **Step 6.** In a new chat session, prompt the agent to use your custom researcher agent: `@researcher Find all TODO comments across the project and categorize them by priority.`

- **Step 7.** Verify the custom agent is executed as a subagent and returned structured results to the primary conversation.

### Checkpoint

1. Did the primary agent spawn a subagent that appeared as a collapsed node in the Chat panel

- [ ] Yes
- [ ] No

2. Were you able to expand and inspect the subagent tool calls and returned summary

- [ ] Yes
- [ ] No

3. Did the custom agent file work as a reusable subagent when invoked with the at-mention

- [ ] Yes
- [ ] No

### Explore more

- [Subagents in VS Code](https://code.visualstudio.com/docs/copilot/agents/subagents)

- [Custom agents overview](https://code.visualstudio.com/docs/copilot/customization/custom-agents)

