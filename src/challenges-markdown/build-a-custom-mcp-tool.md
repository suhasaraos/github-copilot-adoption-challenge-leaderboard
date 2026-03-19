---
Title: Build a custom MCP tool
ActivityId: 12
---

### Summary

In this advanced exercise you will fork your organisation’s local **Model-Context-Protocol (MCP) server**, add a custom “Jira issue creator” tool, and then call that tool from **GitHub Copilot Agent mode** to open a real Jira ticket. Set aside about thirty minutes.

### What you will learn

- Forking and running a local MCP server repository.

- Implementing a new tool endpoint that calls the Jira REST API.

- Registering the tool in `tools/index.ts` (or `.py`) so Agents can discover it.

- Invoking the tool from Copilot Agent mode in VS Code to create a live Jira issue.

### Before you start

Install the latest **GitHub Copilot** extension in VS Code (enable the Pre-Release channel for the newest features). Ensure Agent mode is active in Copilot Chat. Make sure you have: 1. An organisation repo named `mcp-server` (or similar) you can fork. 2. A Jira cloud instance and a personal API token. 3. `node` or `python` (match the server's language) installed locally.

### Steps

- **Step 1.** Fork `github.com/your-org/mcp-server` to your account then clone the fork locally. Run `npm install` or `pip install -r requirements.txt` as required.

- **Step 2.** In the `tools/` folder create `jiraIssueCreator.js`. Implement a function that accepts `title` and `description` then POSTs to `https://your-domain.atlassian.net/rest/api/3/issue` using your API token.

- **Step 3.** Export the tool in `tools/index.js` under the key `"create_jira_issue"` with a JSON schema describing the fields.

- **Step 4.** Start the MCP server locally with `npm start` or `python main.py`. Verify `http://localhost:4891/health` returns `{"status":"ok"}`.

- **Step 5.** In VS Code, open (or create) `.vscode/mcp.json` and register the server:

```json
{
  "servers": {
    "jira-mcp": {
      "type": "http",
      "url": "http://localhost:4891"
    }
  }
}
```

Save the file and reload the VS Code window (`Developer: Reload Window` from the Command Palette).

- **Step 6.** Open any file and switch to Copilot Agent mode. Type `Create a Jira bug: User cannot reset password` and press Enter.

- **Step 7.** The Agent calls `create_jira_issue`. Accept the tool call preview, wait for success, then check Jira for the new ticket.

- **Step 8.** Commit your tool code to the fork and open a pull request back to the organisation repo.

### Checkpoint

1. Did the MCP server start locally and pass the health check

- [ ] Yes
- [ ] No

2. Did the Agent successfully create a live Jira ticket via the new tool

- [ ] Yes
- [ ] No

3. Did you push the custom tool to a branch and open a pull request

- [ ] Yes
- [ ] No

### Explore more

- [Copilot MCP project overview](https://docs.github.com/en/copilot/customizing-copilot/extending-copilot-chat-with-mcp)

- [Copilot Agents overview](https://code.visualstudio.com/docs/copilot/chat/chat-agent-mode)
