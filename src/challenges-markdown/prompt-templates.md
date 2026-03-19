---
Title: Prompt Templates
ActivityId: 12
---

### Summary

In this challenge, you will create a **.prompt.md** file that leverages a high-reasoning model in GitHub Copilot to deeply analyze multi-step code logic, identify edge cases, and suggest improvements. You will also validate that the prompt is discovered by Copilot Chat, that the appropriate model is selected, and that outputs are specific and actionable. Estimated time: ~20 minutes.

### What you will learn

- Creating reusable prompt templates in GitHub Copilot and structuring them for repeatable analysis.
- Triggering advanced model routing for complex reasoning tasks and confirming the active model in the Copilot Chat model picker.
- Re-using and iterating on prompts across different code snippets to drive concrete refactors and tests.

### Before you start

Ensure you are signed in to GitHub in VS Code, have the latest GitHub Copilot extension installed, and access to a reasoning model (such as **GPT-5 mini** or similar models available in Copilot) via the Copilot Chat model picker. Keep a small snippet (30–120 lines) ready for analysis and make sure the repo opens cleanly in VS Code.

### Steps

- **Step 1.** Create the folder `.github/prompts` at the repository root (create both directories if they don’t exist) and commit the empty folder with a placeholder file if required by your tooling.
- **Step 2.** Add a new file `code-logic-explorer.prompt.md` containing a short title, a one-line goal, variables like `{{CODE_SNIPPET}}`, and explicit instructions, e.g., “analyze control flow, error handling, state transitions, time/space complexity, edge cases, and propose test cases.” Save the file.
- **Step 3.** Commit the file and ensure Copilot discovers it: in VS Code, reload the window (Command Palette → “Developer: Reload Window”) or reopen the workspace so the prompt catalog refreshes.
- **Step 4.** Open Copilot Chat and load the prompt. Use the prompt picker to select *code-logic-explorer* or reference it directly (e.g., “Use *code-logic-explorer* with the following code”). Paste your code into the `{{CODE_SNIPPET}}` slot using triple backticks.
- **Step 5.** Select a model: open the model picker in Copilot Chat and choose a reasoning model such as **GPT-5 mini** or similar models available in Copilot for deeper analysis. If unsure, the default model also works well for most prompts.
- **Step 6.** Guide specificity: if the analysis feels generic, ask for concrete outputs—e.g., “list 5 edge cases with inputs/expected outputs,” “provide 3 unit tests in my test framework,” “explain risk of race conditions with a minimal example,” “estimate time/space complexity and cite hot paths.”
- **Step 7.** Apply changes safely: create a new branch, implement the highest-value refactors suggested, and run your test suite (or create tests from the generated cases). Capture any performance or readability improvements.
- **Step 8.** Iterate: re-run the same prompt against the updated code to verify that previously flagged issues are resolved and to surface second-order issues (e.g., error propagation, boundary conditions, I/O timeouts).
- **Step 9.** Reuse: run the prompt on a second snippet (e.g., a sibling module or related function) to confirm portability. Tweak the prompt wording only if you consistently see gaps in the analysis.
- **Step 10.** Troubleshooting quick fixes: if the desired model isn't available — check your Copilot plan or ask your administrator to enable additional models in organisation settings; if the prompt file isn't recognized — verify the path is exactly `.github/prompts/`, the file ends with `.prompt.md`, commit and reload VS Code; if the output is too generic — narrow scope (smaller snippet), request numbered findings with code pointers and test cases, and ask for "fail-first" examples.

### Checkpoint

1. Was the prompt template discovered (correct path/name) and executed with your `{{CODE_SNIPPET}}`?

- [ ] Yes
- [ ] No

2. Did Copilot use an advanced reasoning model and produce specific, testable recommendations (edge cases, tests, refactors)?

- [ ] Yes
- [ ] No

### Explore more

- [Customizing Copilot with prompt files in VS Code](https://code.visualstudio.com/docs/copilot/copilot-customization)
- [Deep dives and tips on the GitHub Copilot Blog](https://github.blog/)
