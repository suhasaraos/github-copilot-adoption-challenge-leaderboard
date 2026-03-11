---
Title: Slash @terminal debugging
ActivityId: 12
---

### Summary

In this debugging exercise you will intentionally break a script in your repository then use **@terminal** in GitHub Copilot Chat to diagnose and fix the failure. Expect to spend fifteen to twenty minutes.

### What you will learn

- Capturing the last terminal command context with `@terminal`.

- Requesting an explanation of an error from Copilot.

- Accepting and applying an AI suggested fix.

- Verifying the script works after the fix.

### Prerequisites

Confirm Copilot Chat is enabled in VS Code or Visual Studio. Open a repository that contains a script you can safely modify for example a Bash or Python utility.

### Steps

- **Step 1.** Open a script or a code file and introduce a simple error such as a misspelled variable name or missing import.

- **Step 2.** Save the file then run the script or build the code in the integrated terminal.

- **Step 3.** The script should fail and show an error message. Do not fix it manually.

- **Step 4.** Open Copilot Chat and type `@terminal why did the last command fail` then press Enter.

- **Step 5.** Read Copilot's explanation and review the suggested fix (if any). If acceptable copy or accept the code into your script or code.

- **Step 6.** Rerun the script or code with the same command. Confirm it now completes successfully.

- **Step 7.** Commit the corrected script with a clear message drafted by you or suggested by Copilot.

### Checkpoint

1. Did Copilot correctly explain the reason for the failure

- [ ] Yes
- [ ] No

2. Did you apply the Copilot suggested fix without manual changes

- [ ] Yes
- [ ] No

3. Did the script run successfully after the fix

- [ ] Yes
- [ ] No

### Explore more

- [VS Code chat tools](https://code.visualstudio.com/docs/copilot/reference/copilot-vscode-features#_chat-tools)
