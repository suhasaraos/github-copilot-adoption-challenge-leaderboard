---
Title: Agent Hooks
ActivityId: 12
---

### Summary

In this exercise you will create **agent hooks** that run custom scripts at key points during an agent session lifecycle. Hooks let you enforce real-world policies such as secret scanning and compliance logging, inject sprint or project context automatically, and trigger automated tests whenever the agent modifies source files. Expect to spend thirty to forty-five minutes.

### What you will learn

- Understanding the eight agent lifecycle events available for hooks.
- Creating a hook configuration file in `.github/hooks/`.
- Writing a `PreToolUse` hook that scans file content for hardcoded secrets using regex patterns and blocks the write if any are found.
- Writing a `PostToolUse` hook that automatically runs unit tests whenever the agent modifies a source file.
- Using a `SessionStart` hook to inject sprint and project context from a `CONTEXT.md` file into every agent session.
- Using the `/create-hook` slash command to generate a `UserPromptSubmit` hook that logs every agent prompt to a local file.

### Before you start

Ensure VS Code has the latest GitHub Copilot Chat extension with agent mode enabled. Have a repository where you can add files to `.github/hooks/`. Shell scripting basics will be helpful, as will familiarity with a test runner such as `npm test` or `pytest`.

### Steps

- **Step 1.** Create the folder `.github/hooks/` in your repository.

- **Step 2.** Create a file `.github/hooks/secret-scanner.json` with the `event` set to `PreToolUse` and a `command` that runs a shell script. The shell script should read the file content (passed via stdin or arguments) and use regex patterns to detect common secret formats such as `ghp_`, `AKIA`, `sk-`, and `password\s*=`. If a match is found the script should exit with a non-zero code to block the write; otherwise it should exit zero to allow it.

- **Step 3.** Start an Agent mode session and ask Copilot to write a file containing a fake API key such as `ghp_faketoken123`. Verify that the hook blocks the write and that the agent reports the operation was rejected.

- **Step 4.** Create a file `.github/hooks/test-runner.json` with the `event` set to `PostToolUse` and a `command` that runs a shell script. The shell script should inspect the modified file's extension and, if it is `.js`, `.ts`, or `.py`, automatically run the appropriate test command (`npm test`, `pytest`, etc.).

- **Step 5.** Ask the agent to modify a source file in your project. Verify that the test runner hook fires automatically after the edit and that the test output appears in the terminal.

- **Step 6.** Create a `CONTEXT.md` file in the repository root containing project context such as current sprint goals, architecture notes, and coding standards. Then create `.github/hooks/context-injector.json` with the `event` set to `SessionStart`. The accompanying script should read `CONTEXT.md` and output its content so that it is injected as context at the start of every agent session.

- **Step 7.** Use the `/create-hook` slash command in Copilot Chat to generate a `UserPromptSubmit` hook. The hook should log every prompt sent to the agent — including a timestamp — to a local `agent-prompt-log.txt` file.

### Checkpoint

1. Did the PreToolUse secret scanner hook block a file write containing a fake API key

- [ ] Yes
- [ ] No

2. Did the PostToolUse hook automatically run tests after a source file was modified

- [ ] Yes
- [ ] No

3. Did the SessionStart hook successfully inject context from `CONTEXT.md`

- [ ] Yes
- [ ] No

4. Were you able to create a `UserPromptSubmit` logging hook using the `/create-hook` command

- [ ] Yes
- [ ] No

### Explore more

- [Agent hooks in VS Code](https://code.visualstudio.com/docs/copilot/customization/hooks)

- [Agent tools and approval](https://code.visualstudio.com/docs/copilot/agents/agent-tools)

- [Custom instructions in VS Code](https://code.visualstudio.com/docs/copilot/customization/custom-instructions)

