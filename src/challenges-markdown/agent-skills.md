---
Title: Agent Skills
ActivityId: 12
---

### Summary

In this exercise you will create a custom **agent skill** by writing a `SKILL.md` file that teaches GitHub Copilot domain-specific knowledge. Skills let Copilot automatically discover and apply your team's practices without you writing explicit prompts every time. Expect to spend twenty to thirty minutes.

### What you will learn

- Understanding the SKILL.md format with YAML front matter and instructions body.
- Creating a skill that Copilot automatically invokes when relevant.
- Using the `/create-skill` slash command to generate a skill with AI assistance.
- Organizing skills at the repository, user, and workspace levels.

### Before you start

Ensure you have VS Code with the latest GitHub Copilot Chat extension. Agent mode is enabled by default in recent versions (no settings change required). Have a repository where you can add files to the `.github/skills/` directory.

### Steps

- **Step 1.** Create the folder `.github/skills/` in your repository if it does not already exist.

- **Step 2.** Create a file called `.github/skills/code-review/SKILL.md` with the following structure: YAML front matter containing `name` and `description` fields, followed by a body with instructions for performing code reviews according to your team's standards. For example, instruct Copilot to check for error handling, verify input validation, and flag any hardcoded secrets.

- **Step 3.** Open Copilot Chat in Agent mode. Enter a prompt that would naturally trigger the skill, such as: `Review the changes in src/auth/login.ts for any issues.`

- **Step 4.** Observe that Copilot reads and applies your skill instructions automatically. The skill name should appear in the chat response or tool call log showing it was activated.

- **Step 5.** Now use the AI-assisted approach. Type `/create-skill` in Copilot Chat and follow the prompts to generate a new skill for a different domain, such as database migration best practices.

- **Step 6.** Optionally create a user-level skill by placing a `SKILL.md` file under `~/.copilot/skills/my-skill/SKILL.md`. This skill will be available across all your repositories.

- **Step 7.** Test that both skills activate correctly by entering prompts that match each skill's description.

### Checkpoint

1. Did Copilot automatically discover and apply the SKILL.md instructions during a relevant prompt

- [ ] Yes
- [ ] No

2. Did the /create-skill command successfully generate a new skill file

- [ ] Yes
- [ ] No

3. Were you able to organize skills at the repository or user level and verify they activated

- [ ] Yes
- [ ] No

### Explore more

- [Agent skills in VS Code](https://code.visualstudio.com/docs/copilot/customization/agent-skills)

- [Agent Skills open standard](https://agentskills.io)

