---
Title: Slash command speed run
ActivityId: 12
---

### Summary

This exercise shows how to use the **/tests** slash command in GitHub Copilot Chat to generate unit tests for code you just changed. You will invoke the command, accept the resulting test file, then run your usual test runner to confirm everything passes. Expect to finish in fifteen to twenty minutes.

### What you will learn

- Opening Copilot Chat and issuing a slash command.

- Letting Copilot generate language specific unit tests.

- Saving the generated file into your repository.

- Running your existing test framework to validate results.

### Prerequisites

Ensure GitHub Copilot Chat is enabled in **your IDE** (VS Code JetBrains Visual Studio or Neovim). Confirm your project has a working test runner such as Jest PyTest JUnit or xUnit. Open a file you modified in the last commit.

### Steps

- **Step 1.** Place the cursor inside the file you recently edited. No need to select text.

- **Step 2.** Open Copilot Chat. In VS Code click the Copilot icon in the Activity Bar and open a new local chat session, Agent or Ask mode. In JetBrains choose **View > Tool Windows > Copilot Chat**.

- **Step 3.** In the chat box type `/tests` and press Enter. Copilot will respond with a proposed test file tailored to the file under the cursor.

- **Step 4.** Review the generated code. Use **Tab** to accept or **Esc** to reject and retry. If accepted Copilot inserts the new test file in the correct folder.

- **Step 5.** Run your normal test command such as `npm test` `pytest` or `dotnet test`. Confirm that all tests pass. **Tip** - Ask Copilot about how to run the tests manually

- **Step 6.** Commit the new test file with a clear message. You can let Copilot draft the commit message if you pause at the prompt.

### Checkpoint

1. Did Copilot create a test file without manual coding

- [ ] Yes
- [ ] No

2. Did the generated tests run successfully in your test framework

- [ ] Yes
- [ ] No

3. Did you review and adjust any part of the generated tests before committing

- [ ] Yes
- [ ] No

### Explore more

- [Copilot Chat slash commands reference](https://code.visualstudio.com/docs/copilot/reference/copilot-vscode-features#_slash-commands)

- [Generating tests with Copilot tutorial](https://docs.github.com/en/copilot/using-github-copilot/guides-on-using-github-copilot/writing-tests-with-github-copilot)
