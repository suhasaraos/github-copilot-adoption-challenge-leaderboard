---
Title: CLI suggest & execute
ActivityId: 12
---

### Summary

In this exercise you will use **GitHub Copilot CLI** to suggest and run a command that cleans up local branches already merged into main. You will execute the suggestion then verify the result with `git branch`. The task should take about ten minutes.

### What you will learn

- Running `copilot -p` to generate shell commands directly in the terminal.

- Executing the generated command directly from the CLI prompt.

- Verifying local branch cleanup with `git branch`.

- Using Copilot CLI for other repository maintenance tasks.

### Prerequisites

Confirm that the latest version of Copilot CLI is installed with `copilot --version`. Use the `-p` flag for non-interactive, direct output. Authenticate with `gh auth login` and open a terminal in the root of a repository that has multiple merged branches.

### Steps

- **Step 1.** In the project root run the following command and press Enter.

`copilot -p "clean local merged branches"`

- **Step 2.** Copilot CLI prints the suggested command directly in the terminal — it should resemble `git branch --merged | grep -v "main" | xargs -n 1 git branch -d`.

- **Step 3.** Copy the suggested command, paste it into your terminal, and run it.

- **Step 4.** After execution run `git branch` to list local branches and confirm that only active branches remain.

- **Step 5.** Explore other cleanup ideas by running `copilot -p "list large files in repo"` or a command of your choice. You might need to use Administrative permissions or `sudo` for this to work.

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
