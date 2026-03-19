---
Title: Automated PR review rules
ActivityId: 12
---

### Summary

In this challenge, you'll enable GitHub Copilot's **Review changes** feature to provide AI-generated suggestions on your pull requests. After configuring your repository to use Copilot for reviews, you'll open a pull request and see Copilot's inline review comments in action. This activity takes about fifteen minutes. Note that some of the features in this challenge requires **GitHub Copilot Enterprise.**

### What you will learn

- How to enable GitHub Copilot’s review features for pull requests.

- How to interpret Copilot-generated feedback and suggestions.

- How to act on Copilot’s comments by updating your code or replying in threads.

- How to complete the review and merge process with both AI and human approvals.

### Before you start

You need **GitHub Copilot** access and appropriate permissions to open pull requests in the target repository. Ensure that GitHub Copilot is enabled for your account and the repository. The **Review changes** feature uses premium Copilot requests.

### Steps

- **Step 1.** In the browser, open the repository and create a new branch using `git checkout -b copilot-review-test`. Make a small change in the code.

- **Step 2.** Push the branch to GitHub and open a pull request as usual.

- **Step 3.** In the pull request view, click the **Copilot** button and select **Review changes**.

- **Step 4.** Wait for Copilot to generate its review. Copilot will leave inline comments with suggested improvements, bug fixes, or explanations.

- **Step 5.** Respond to at least one comment by either updating your code or replying directly in the conversation.

- **Step 6.** Request a human review if needed, then complete the merge once both AI and human reviewers (if required) have approved.

### Checkpoint

1. Did you successfully initiate a Copilot review on a pull request?

- [ ] Yes
- [ ] No

2. Did Copilot leave any inline comments or suggestions?

- [ ] Yes
- [ ] No

3. Did you respond to at least one suggestion and merge the pull request?

- [ ] Yes
- [ ] No

### Explore more

- [Using Copilot for code review](https://docs.github.com/en/copilot/using-github-copilot/code-review/using-copilot-code-review)

- [Working with pull requests](https://docs.github.com/en/pull-requests)
