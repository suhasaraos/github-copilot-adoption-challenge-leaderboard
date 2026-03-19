---
Title: CLI suggest & execute
ActivityId: 12
---

### Summary

In this exercise you will use **GitHub Copilot in the CLI** (`gh copilot`) to suggest and run a command that cleans up local branches already merged into main. You will review the agent's plan, approve the command, then verify the result with `git branch`. The task should take about ten minutes.

### What you will learn

- Using `gh copilot suggest` to request shell commands from Copilot.

- Using `gh copilot explain` to understand what a command does before running it.

- Reviewing and approving the agent's suggested command before execution.

- Verifying local branch cleanup with `git branch`.

- Using `gh copilot` for other repository maintenance tasks.

### Prerequisites

Confirm that the GitHub CLI is installed with `gh --version`. Install the Copilot extension for the CLI if needed: `gh extension install github/gh-copilot`. Authenticate with `gh auth login` and open a terminal in the root of a repository that has multiple merged branches.

### Steps

- **Step 1.** In the project root run:

`gh copilot suggest "Delete all local branches that have already been merged into main"`

- **Step 2.** Copilot proposes a shell command — it should resemble `git branch --merged | grep -v "main" | xargs -n 1 git branch -d`. Review it before proceeding.

- **Step 3.** When prompted, choose **Execute command** to run it, or copy it and run it manually in the terminal. If you want to understand the command first, run:

`gh copilot explain "git branch --merged | grep -v 'main' | xargs -n 1 git branch -d"`

- **Step 4.** After execution run `git branch` to list local branches and confirm that only active branches remain.

- **Step 5.** Explore other tasks by running `gh copilot suggest "List the largest files in this repo"` or a task of your choice.

- **Step 6.** Commit any auxiliary changes if applicable and push to remote.

### Checkpoint

1. Did Copilot suggest a safe branch cleanup command

- [ ] Yes
- [ ] No

2. Did the command remove local branches that are already merged

- [ ] Yes
- [ ] No

3. Did you confirm the result with `git branch`

- [ ] Yes
- [ ] No

### Explore more

- [GitHub Copilot in the CLI](https://docs.github.com/en/copilot/github-copilot-in-the-cli/about-github-copilot-in-the-cli)
