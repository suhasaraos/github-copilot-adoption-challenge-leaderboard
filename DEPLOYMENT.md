# Leaderboard App - Azure Developer CLI Setup and Deployment Guide

This guide will help you provision the Leaderboard App infrastructure and then deploy the app using Azure Developer CLI (azd).

## Prerequisites

1. Install Azure Developer CLI (azd): https://aka.ms/azd-install
2. Install Azure CLI: https://aka.ms/azcli-install  
3. Have an Azure subscription

## Quick Start (Recommended)

###  One-Command Deployment
__This step is already completed by seed-database.ps1 as part of the README quick start.__

The fastest and easiest way to get started:

```bash
azd auth login
azd up
```

That's it! `azd up` will:
- Initialize your environment
- Provision all Azure infrastructure (VNet, App Service, SQL Database, Key Vault, etc.)
- Build and deploy your .NET application
- Display your application URL

**First-time setup:** When you run `azd up`, you'll be prompted for:
1. **Environment name** (e.g., "dev", "test", "prod")
2. **Azure subscription** (select from your available subscriptions)
3. **Azure location** (e.g., "East US", "West US 2")

> **💡 Tip:** Use `azd up` for the complete experience. It's the simplest way to deploy!

---

### Step-by-Step Deployment (Optional)

If you prefer more control or want to provision and deploy separately:

#### 1. Initialize the environment
```bash
azd auth login
azd init
# Choose "Use code in the current directory" when prompted
```

#### 2. Provision infrastructure only
```bash
azd provision
```

This creates all Azure resources without deploying the application code.

#### 3. Deploy the application only
```bash
azd deploy
```

This builds and deploys your .NET application to the existing infrastructure.

#### Pre-configure parameters (Advanced)

If you want to avoid interactive prompts:

```bash
# Create and select environment
azd env new <environment-name>

# Set required parameters manually
azd env set AZURE_LOCATION "eastus2"

# Then provision
azd provision
```

---

## Post-Deployment Configuration

### Configure SQL Database Permissions (Required)

The deployment now automates SQL permission setup for the App Service managed identity. Run the helper script before `azd up` to collect the necessary details from your current Azure CLI session. __This step is already completed by seed-database.ps1 as part of the README quick start.__

#### One-Time Setup Workflow

1. **Run the helper script** (updates azd environment variables with your identity details):

   ```powershell
   ./setup-azd.ps1 -Location <azure-region> -EnvironmentName <env-name>
   ```

   The script:
   - Authenticates (if needed) and records your Azure AD object ID & UPN
   - Detects your current public IPv4 address and keeps it on the SQL firewall
   - Requests an Azure SQL access token for your account (valid for ~60 minutes) and stores it for the next deployment

2. **Immediately run** `azd up` (or `azd provision`) *within one hour* so that the short-lived SQL access token remains valid.

3. During deployment, the Bicep template will:
   - Set the signed-in user as the SQL Server Entra ID admin
   - Keep your current IPv4 address in the firewall allow list for future administrative access
   - Create the App Service managed identity inside the database and grant the `db_datareader`, `db_datawriter`, and `db_ddladmin` roles automatically

#### Verifying the Result

```sql
SELECT p.name AS Principal,
       r.name AS RoleName
FROM sys.database_role_members rm
JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id
WHERE p.name = '<web_app_name>';
```

You should see the three roles listed for your App Service managed identity. Replace `<web_app_name>` with the value of `WEB_APP_NAME` from `azd env get-values`.

---

#### What These Permissions Do

- **db_datareader**: Allows the app to read data (SELECT queries)
- **db_datawriter**: Allows the app to write data (INSERT, UPDATE, DELETE)
- **db_ddladmin**: Allows the app to create/modify database schema (migrations)

---

#### Troubleshooting SQL Configuration

**"Deny Public Network Access is set to Yes" or "DB Init failed"**

This error appears in App Service logs when the managed identity hasn't been granted database permissions yet:
```
DB Init failed: Reason: Connection was denied because Deny Public Network Access is set to Yes
Login failed for user '<token-identified principal>'
```

**Solution:** Complete Steps 2-5 above to:
1. Temporarily enable public access
2. Set yourself as Entra ID admin
3. Grant the managed identity database permissions
4. Disable public access again

After granting permissions, restart your App Service:
```bash
# Via Azure CLI
az webapp restart --name <app-service-name> --resource-group <resource-group-name>

# Or via Portal: App Service → Overview → Restart
```

The app will then connect successfully through the private endpoint.

---

**"Cannot connect to SQL Server"**
- Make sure you added your IP address in Step 2
- Ensure you're using Microsoft Entra authentication in Query Editor

**"Cannot find the user"**
- Double-check your App Service name matches exactly (case-sensitive)
- Make sure you're using square brackets: `[app-name]`
- Example: `CREATE USER [sunew12Oct3-be2ma] FROM EXTERNAL PROVIDER;`

**"Login failed for user"**
- Verify you set yourself as Entra ID admin in Step 3
- Try logging out and back into the Azure Portal
- Ensure you completed the permissions grant in Step 4

**"Principal 'xxx' could not be found"**
- The App Service managed identity may not be fully propagated yet
- Wait 1-2 minutes and try again
- Verify the App Service has system-assigned managed identity enabled

---

### Initialize Scoring System (Required)

After granting SQL permissions and before using the application, you must initialize the Activities table with the default scoring system. This defines how different GitHub Copilot activities are weighted and scored. __This step is already completed by seed-database.ps1 as part of the README quick start.__

> **⚠️ Important:** This is a one-time setup that must be done after the database is accessible but before challenges can be populated.

#### Step 1: Access Query Editor

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your **SQL Server** → **SQL databases** → **leaderboarddb**
3. Click **Query editor (preview)** in the left menu
4. Click **Continue as \<your-email\>** to login using **Microsoft Entra authentication**

#### Step 2: Initialize Activities Table

Copy and paste the following SQL script into the query window:

```sql
SET IDENTITY_INSERT Activities ON;

INSERT INTO Activities (ActivityId, Name, WeightType, Weight, Scope, Frequency) VALUES
(1, 'ActiveUsersPerDay', 'Multiplier', 50.00, 'User', 'Daily'),
(2, 'EngagedUsersPerDay', 'Multiplier', 75.00, 'User', 'Daily'),
(3, 'TotalCodeSuggestions', 'Multiplier', 1.00, 'Team', 'Daily'),
(4, 'TotalLinesAccepted', 'Multiplier', 1.50, 'Team', 'Daily'),
(5, 'TotalChats', 'Multiplier', 10.00, 'Team', 'Daily'),
(6, 'TotalChatInsertions', 'Multiplier', 50.00, 'Team', 'Daily'),
(7, 'TotalChatCopyEvents', 'Multiplier', 30.00, 'Team', 'Daily'),
(8, 'TotalPRSummariesCreated', 'Multiplier', 100.00, 'Team', 'Daily'),
(9, 'TotalDotComChats', 'Multiplier', 20.00, 'Team', 'Daily'),
(10, 'CompletedLearningModule', 'Fixed', 250.00, 'User', 'Once'),
(11, 'GitHubCopilotCertificationExam', 'Fixed', 1000.00, 'User', 'Once'),
(12, 'CopilotDailyChallengeCompleted', 'Fixed', 200.00, 'User', 'Daily'),
(13, 'LinkClicked', 'Fixed', 50.00, 'User', 'Once'),
(14, 'TeamBonus', 'Fixed', 50.00, 'Team', 'Once');

SET IDENTITY_INSERT Activities OFF;
```

Click **Run** (or press F5) and verify you see a success message.

#### Understanding the Scoring System

The Activities table defines how different GitHub Copilot activities contribute to team scores:

**Weight Types:**
- **Multiplier**: Score = Activity count × Weight (e.g., 10 chats × 10.00 = 100 points)
- **Fixed**: Score = Weight regardless of count (e.g., completing certification = 1000 points)

**Scopes:**
- **User**: Individual participant activity
- **Team**: Aggregated team activity

**Frequencies:**
- **Daily**: Activity tracked and scored every day
- **Once**: One-time achievement (e.g., certifications)

**Default Weights (Customizable):**

| Activity | Weight | Type | Why It Matters |
|----------|--------|------|----------------|
| ActiveUsersPerDay | 50.00 | Multiplier | Encourages daily engagement |
| EngagedUsersPerDay | 75.00 | Multiplier | Rewards active participation |
| TotalCodeSuggestions | 1.00 | Multiplier | Tracks basic usage |
| TotalLinesAccepted | 1.50 | Multiplier | Rewards accepting suggestions |
| TotalChats | 10.00 | Multiplier | Encourages using Chat features |
| TotalChatInsertions | 50.00 | Multiplier | Rewards code insertions from Chat |
| TotalChatCopyEvents | 30.00 | Multiplier | Tracks code copying from Chat |
| TotalPRSummariesCreated | 100.00 | Multiplier | Encourages PR documentation |
| TotalDotComChats | 20.00 | Multiplier | Rewards using GitHub.com Chat |
| CompletedLearningModule | 250.00 | Fixed | Recognizes training completion |
| GitHubCopilotCertificationExam | 1000.00 | Fixed | Highest reward for certification |
| CopilotDailyChallengeCompleted | 200.00 | Fixed | Daily challenge incentive |
| LinkClicked | 50.00 | Fixed | Engagement tracking |
| TeamBonus | 50.00 | Fixed | Team collaboration bonus |

> **💡 Customization Tip:** You can adjust these weights based on what's most important to your organization. For example:
> - Increase `GitHubCopilotCertificationExam` to 2000 to emphasize certification
> - Increase `TotalPRSummariesCreated` to encourage better documentation
> - Adjust `TotalChats` vs `TotalCodeSuggestions` to prioritize different features

#### Step 3: Verify Initialization

Run this query to confirm the activities were inserted:

```sql
SELECT ActivityId, Name, WeightType, Weight, Scope, Frequency 
FROM Activities 
ORDER BY ActivityId;
```

You should see 14 rows returned with all the activities listed above.

---

### Initialize Sample Challenges (Optional)

After initializing the scoring system, you can optionally add sample challenges to help perform dry runs. The application includes a comprehensive library of challenges covering various GitHub Copilot features and use cases.

> **⚠️ Important:** You will need to customize these challenges based on what features are available in your environment. Some challenges may reference preview features or GitHub Copilot Enterprise capabilities that might not be enabled for your organization.

**Before adding challenges:**
- Review the available challenge types in the [challenges markdown folder](./src/challenges-markdown/)
- Identify which GitHub Copilot features are enabled for your organization
- Remove or modify challenges that reference unavailable features
- Consult with your GitHub Copilot administrator about feature availability

**Challenge Categories Available:**

The full script includes challenges across multiple categories:

1. **Getting Started** - Ghost Text 101, Say hi to Copilot Chat, Docstring on demand
2. **CLI Tools** - CLI explain, CLI suggest & execute
3. **Slash Commands** - Slash command speed run, @terminal debugging
4. **Code Review** - Language model pair review, Explain my PR, Commit message concierge
5. **Refactoring** - Refactor with confidence, Cross file rename
6. **Advanced Features** - Vision preview, Copilot Workspace, Custom instructions
7. **Security** - Security lens starter
8. **Documentation** - README booster, Generate documentation
9. **Testing** - Slash /tests command
10. **Specialized** - Copilot for SQL, Internationalization, Performance profiling
11. **Enterprise Features** - Enterprise policy testing, Context augmented agent, Custom chat modes

#### Adding Sample Challenges

**Option 1: Add Selected Challenges (Recommended)**

1. Review the challenge script at [challenges markdown folder](./src/challenges-markdown/)
2. DELETE the challenge markdown file that you do NOT want to add. (don't worry, you can always undo file deletion via git) and INSERT the challenges into the DB either using Option a) or b) below
   
   a. [**Automatic** - INSERT using Python script] Run the follow python script ```./src/challenges-util/generate_sql_inserts.py``` to generate INSERT statements for only the remaining challenges. Create a virtual environment to install the required packages.
   
   ```bash
   python3 -m venv ghcp
   source ghcp/bin/activate
   pip install markdown
   python ./src/challenges-util/generate_sql_inserts.py
   ```
   
   b. [**Manual**  - INSERT using DB Query Editor] Copy the content in [src/challenges-util/challenges-insert.sql](./src/challenges-util/challenges-insert.sql) and execute them in Query Editor as shown in the scoring system setup OR just run ``` ./src/infra/scripts/run-challenges-insert.ps1 ``` from your local repo to execute it automatically

**Option 2: Add All Challenges (Requires Review)**

If you want to add all challenges initially and remove unavailable ones later:

1. Go to Azure Portal → SQL Server → SQL databases → leaderboarddb
2. Click **Query editor (preview)** and authenticate
3. Open the full script from your local repository: [src/challenges-util/challenges-insert.sql](./src/challenges-util/challenges-insert.sql)
4. Copy and execute the INSERT statements OR just run ``` ./src/infra/scripts/run-challenges-insert.ps1 ``` from your local repo to execute it automatically
5. Review the challenges in the application and deactivate any that aren't applicable

**Example - Adding Just 3 Starter Challenges:**

```sql
INSERT INTO [dbo].[Challenges] (Title, Content, PostedDate, ActivityId) VALUES 
(N'Ghost Text 101', N'<challenge content here>', SYSDATETIME(), 12),
(N'Say hi to Copilot Chat', N'<challenge content here>', SYSDATETIME(), 12),
(N'Docstring on demand', N'<challenge content here>', SYSDATETIME(), 12);
```

> **📋 Customization Plan:** We recommend scheduling time with your team to:
> 1. Walk through all available challenges in [src/challenges-util/challenges-insert.sql](./src/challenges-util/challenges-insert.sql)
> 2. Test each challenge type in your environment
> 3. Create a custom script with only applicable challenges
> 4. Document which features are available for your organization
> 5. Set a review cadence to add new challenges as features are enabled

**Features to Verify Before Adding Challenges:**

| Feature | Required For | How to Check |
|---------|-------------|--------------|
| GitHub Copilot Enterprise | Vision Preview, Workspace, Knowledge Base challenges | Check your GitHub organization settings |
| Copilot CLI | CLI-related challenges | Try `gh copilot --version` |
| Copilot Chat | Chat-based challenges | Available in VS Code with Copilot extension |
| Code Review | PR review challenges | GitHub Copilot Enterprise feature |
| MCP Tools | Custom tool challenges | Check with your Copilot administrator |

> **💡 Tip:** Start with basic challenges (Ghost Text, Chat, Docstrings) that work with all Copilot tiers, then add advanced challenges as features become available to your organization.

---

### Configure Azure AD Authentication (Required)

After configuring SQL permissions, you must set up Azure AD authentication to enable user sign-in for the application.  __This step is already completed by app-reg-setup.ps1 as part of the README quick start.__

> **⚠️ Important:** The application uses Microsoft Entra ID (Azure AD) for authentication. You must register an App Registration and configure the App Service with the ClientId, TenantId, and ClientSecret.

#### Step 1: Create an App Registration

1. Go to [Azure Portal](https://portal.azure.com)
2. Search for **App registrations** in the top search bar
3. Click **+ New registration**
4. Configure the registration:
   - **Name**: `LeaderboardApp` (or your preferred name)
   - **Supported account types**: Select **Accounts in this organizational directory only (Single tenant)**
   - **Redirect URI**: 
     - Platform: **Web**
     - URI: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
     - (Replace `<your-app-service-name>` with your actual App Service name from `azd env get-values`)
5. Click **Register**

#### Step 2: Configure Authentication Settings

1. In your newly created App Registration, click **Authentication** in the left menu
2. Under **Platform configurations**, you should see the Web redirect URI you added
3. Scroll down to **Implicit grant and hybrid flows** section
4. Check the following boxes:
   - ☑ **Access tokens** (used for implicit flows)
   - ☑ **ID tokens** (used for implicit and hybrid flows)
5. Click **Save** at the top

> **Note:** These token settings enable the application to receive tokens from Azure AD during the authentication flow.

#### Step 3: Create a Client Secret

1. In your App Registration, click **Certificates & secrets** in the left menu
2. Under **Client secrets**, click **+ New client secret**
3. Add a description (e.g., "LeaderboardApp Secret")
4. Select an expiration period (e.g., **180 days**, **1 year**, **2 years**)
5. Click **Add**
6. **⚠️ IMPORTANT:** Copy the **Value** immediately (it will only be shown once!)

#### Step 4: Get Required Values

You'll need three values from your App Registration:

1. **ClientId**: 
   - Go to **Overview** in your App Registration
   - Copy the **Application (client) ID**

2. **TenantId**: 
   - Still on the **Overview** page
   - Copy the **Directory (tenant) ID**

3. **ClientSecret**: 
   - The value you copied in Step 3

4. **Domain** (optional):
   - Still on the **Overview** page
   - Copy the **Primary domain** (e.g., `yourcompany.onmicrosoft.com`)

#### Step 5: Configure App Service Environment Variables

Now add these values to your App Service via Azure Portal:

1. Go to your **App Service** in the Azure Portal
2. In the left menu, click **Environment variables** under **Settings**
3. Click **+ Add** to add each variable:

| Name | Value |
|------|-------|
| `AzureAd__ClientId` | Your Application (client) ID |
| `AzureAd__TenantId` | Your Directory (tenant) ID |
| `AzureAd__ClientSecret` | Your client secret value |
| `AzureAd__Domain` | Your primary domain (e.g., `yourcompany.onmicrosoft.com`) |

4. Click **Apply** at the bottom
5. Click **Confirm** when prompted (this will restart your app)

#### Step 6: Verify Authentication

1. Wait ~1 minute for the App Service to restart
2. Navigate to your app URL: `https://<your-app-service-name>.azurewebsites.net`
3. You should be redirected to the Microsoft sign-in page
4. Sign in with your Azure AD credentials
5. You should be redirected back to the app after successful authentication

---

#### Troubleshooting Azure AD Configuration

**"IDW10106: The 'ClientId' option must be provided"**
- Ensure you added the `AzureAd__ClientId` environment variable
- Verify the App Service has restarted (check **Restart** in App Service Overview)

**"Redirect URI mismatch"**
- Go to your App Registration → **Authentication** → **Web Redirect URIs**
- Verify the URL matches: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
- Make sure to include `/signin-oidc` at the end

**"Authentication failed" or token-related errors"**
- Verify you enabled **Access tokens** and **ID tokens** in Step 2
- Go to App Registration → **Authentication** → **Implicit grant and hybrid flows**
- Ensure both checkboxes are checked

**"Client secret expired"**
- Create a new client secret in App Registration → **Certificates & secrets**
- Update the `AzureAd__ClientSecret` environment variable in App Service

**"User cannot sign in"**
- Verify the user account exists in your Azure AD tenant
- Check App Registration → **API permissions** for any required permissions

---

### Optional Configuration

The following configurations are optional and can be set up based on your needs.

---

#### GitHub Integration (Optional)

The application can integrate with GitHub to track Copilot usage and team activities. This is **disabled by default** and not required for basic operation.

**When to enable:**
- You want to track GitHub Copilot usage metrics
- You want to validate challenge completions via GitHub activity
- You want team creation to sync with GitHub organizations

**Requirements:**
- GitHub organization with Copilot enabled
- Personal Access Token (PAT) with required scopes:
  - `repo` (if accessing private repos)
  - `admin:org` (for team management)
  - `user` (for user information)

**Setup Steps:**

1. **Create GitHub PAT:**
   - Go to GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
   - Generate new token with scopes: `repo`, `admin:org`, `user`
   - Copy the token value immediately

2. **Store PAT in Key Vault:**
   ```bash
   # Get Key Vault name
   azd env get-values | grep KEY_VAULT_NAME
   
   # Store PAT in Key Vault
   az keyvault secret set --vault-name <key-vault-name> --name "GitHub-PAT" --value "<your-pat-token>"
   ```

3. **Configure App Service:**
   - Go to your App Service → **Environment variables**
   - Add the following:

   | Name | Value |
   |------|-------|
   | `GitHubSettings__Enabled` | `true` |
   | `GitHubSettings__Org` | Your GitHub organization name |
   | `GitHubSettings__PAT` | `@Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/GitHub-PAT/)` |

4. Click **Apply** → **Confirm**

> **Note:** If GitHub integration is disabled (`false`), the app operates in "disconnected mode" where teams are managed only in the local database and no GitHub metrics are tracked.

---

#### SMTP Configuration (Optional)

Configure SMTP to enable email notifications for account management and participant communications.

**When to enable:**
- You want to send passcode emails to participants
- You want to send team invitations
- You want to send challenge notifications

**Setup Steps:**

1. **Get SMTP Credentials:**
   - Use your organization's SMTP server (e.g., Office 365, Gmail, SendGrid)
   - Note: Server, Port, Username, Password

2. **Store SMTP Password in Key Vault:**
   ```bash
   az keyvault secret set --vault-name <key-vault-name> --name "SMTP-Password" --value "<your-smtp-password>"
   ```

3. **Configure App Service:**
   - Go to your App Service → **Environment variables**
   - Add the following:

   | Name | Value |
   |------|-------|
   | `SmtpSettings__Server` | Your SMTP server (e.g., `smtp.office365.com`) |
   | `SmtpSettings__Port` | SMTP port (typically `587` for TLS) |
   | `SmtpSettings__SenderName` | Display name for emails |
   | `SmtpSettings__SenderEmail` | Email address to send from |
   | `SmtpSettings__Username` | SMTP username |
   | `SmtpSettings__Password` | `@Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/SMTP-Password/)` |
   | `SmtpSettings__EnableSsl` | `true` |

4. Click **Apply** → **Confirm**

**Common SMTP Providers:**

| Provider | Server | Port | Notes |
|----------|--------|------|-------|
| Office 365 | smtp.office365.com | 587 | Requires app password if MFA enabled |
| Gmail | smtp.gmail.com | 587 | Requires app password |
| SendGrid | smtp.sendgrid.net | 587 | Free tier available |
| AWS SES | email-smtp.{region}.amazonaws.com | 587 | Requires SMTP credentials from AWS |

---

#### Challenge Settings (Optional)

These settings control challenge behavior and are pre-configured with defaults. Modify only if needed.

| Setting | Default | Description |
|---------|---------|-------------|
| `ChallengeSettings__MaxParticipantsPerTeam` | `8` | Maximum team size |
| `ChallengeSettings__ChallengeStarted` | `false` | Whether challenge has started |
| `ChallengeSettings__ChallengeStartDate` | `10/04/2025` | Challenge start date |

To modify, update the environment variables in App Service → **Environment variables**.

---

#### Using Key Vault References for Secrets

The deployment follows Azure best practices by storing secrets in Key Vault and referencing them in App Service configuration.

**Key Vault Reference Format:**
```
@Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/<secret-name>/)
```

**Example:**
```
AzureAd__ClientSecret = @Microsoft.KeyVault(SecretUri=https://mykeyvault.vault.azure.net/secrets/AzureAd-ClientSecret/)
```

**Benefits:**
- ✅ Secrets never stored in App Service configuration
- ✅ Automatic secret rotation support
- ✅ Centralized secret management
- ✅ Audit logging via Key Vault
- ✅ Access control via Azure RBAC

**Best Practice for Azure AD Client Secret:**

Instead of setting the client secret directly:
```bash
# ❌ Not recommended (secret stored in App Service)
AzureAd__ClientSecret = "abc123..."

# ✅ Recommended (secret in Key Vault)
# 1. Store in Key Vault:
az keyvault secret set --vault-name <keyvault-name> --name "AzureAd-ClientSecret" --value "<your-client-secret>"

# 2. Reference in App Service:
AzureAd__ClientSecret = @Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/AzureAd-ClientSecret/)
```

---

## What gets provisioned

- Resource Group
- Virtual Network with subnets
- App Service Plan (Premium V2)
- App Service (Linux, .NET 8) with System-Assigned Managed Identity
- Azure SQL Database with Entra ID authentication
- Key Vault for secrets
- Private Endpoints for secure connectivity
- Storage Account
- Virtual Network for secure networking

## Security Features

### Managed Identity Authentication
- **Passwordless SQL Access**: App Service uses its managed identity to connect to SQL Server
- **No Credential Storage**: No passwords stored in configuration or Key Vault
- **Automatic Credential Rotation**: Azure handles all credential management
- **Entra ID Integration**: SQL Server uses Microsoft Entra ID authentication
- **Manual Admin Setup**: Set yourself as Entra ID admin via Azure Portal to grant permissions

### Network Security
- **Private Endpoints**: SQL Database and Key Vault accessible only via private network
- **VNet Integration**: App Service connects to Azure resources through private network
- **IP Restrictions**: Main app protected with IP restrictions (SCM allows deployment)
- **TLS Encryption**: All traffic encrypted in transit

## Accessing the Application

After successful deployment, your Leaderboard App will be available at:
```
https://<environment-name>-<unique-suffix>.azurewebsites.net/
```

The application includes:
- Participant and team management
- Activity-based scoring system  
- Real-time leaderboard updates
- Daily summary tracking
- Microsoft Entra ID authentication
- Secure database connectivity via private endpoints

## Troubleshooting

### Common Issues

1. ** "The 'location' property must be specified" error**:
   
   **Solution**: Manually set the location parameter:
   ```bash
   azd env set AZURE_LOCATION "eastus"  # or your preferred region
   ```
   
   Or set it interactively during provision:
   ```bash
   azd env set AZURE_LOCATION "eastus"  # or your preferred region
   ```

2. ** Resource conflicts from previous failed deployments**:
   
   **Solution**: Clean up any existing resources:
   ```bash
   azd down --force --purge
   ```

3. ** Array index out of bounds in templates**:
   
   This has been fixed in the current templates, but if you encounter this:
   - Ensure you're using the latest template versions
   - Check subnet array indices match the network configuration

4. **"Error 403 - Forbidden" when accessing the application**:

   **Cause**: The App Service has IP restrictions configured to deny all public access by default. This is a security feature to ensure the app is only accessible through the private endpoint within the VNet or from whitelisted IP ranges.

   **Solutions** (in order of recommendation):

   **Option A - Access via VNet or Peered Network (Recommended)**
   
   The application is accessible from:
   - Resources within the same Azure VNet
   - VNets peered to the application's VNet
   - On-premises networks connected via VPN Gateway or ExpressRoute
   - Corporate networks with IP ranges whitelisted in the firewall rules
   
   **Common scenarios:**
   - **Corporate VPN/ExpressRoute**: Employees access through company network with whitelisted IP ranges
   - **Azure Bastion**: Deploy a Bastion host in the VNet for secure browser-based access
   - **Jump Box/VM**: Deploy a VM in the VNet and access the app from there
   - **Application Gateway**: Front the app with Application Gateway in the VNet
   - **Peered VNets**: Access from other Azure services in peered VNets
   
   > **Recommended**: This is the most secure option, especially in corporate scenarios where only specific internal IP ranges should have access.

   **Option B - Add Your IP Address (for Testing)**
   
   Whitelist your specific IP address or corporate IP range:
   
   1. Go to [Azure Portal](https://portal.azure.com)
   2. Navigate to your App Service
   3. In the left menu, go to **Networking** under **Settings**
   4. Click **Access restriction** under **Inbound Traffic**
   5. Click **+ Add rule** under **Site access**
   6. Configure the rule:
      - **Name**: "AllowMyIP" (or "AllowCorporateNetwork")
      - **Priority**: 50 (must be lower than 100 to take precedence)
      - **Action**: Allow
      - **Type**: IPv4
      - **IP Address Block**: Enter your IP address or IP range in CIDR notation
        - Single IP: `203.0.113.45/32`
        - IP Range: `203.0.113.0/24` (for corporate networks)
   7. Click **Add rule** then **Save**
   8. Wait ~30 seconds and refresh your browser
   
   > **💡 Tip**: You can find your public IP by visiting https://whatismyipaddress.com
   >
   > **Use Case**: Best for individual developers testing or corporate environments where you want to whitelist specific office IP ranges.

   **Option C - Remove IP Restrictions (Least Recommended)**
   
   ⚠️ **Only use for temporary testing!**
   
   1. Go to [Azure Portal](https://portal.azure.com)
   2. Navigate to your App Service (e.g., `myapp-abc123`)
   3. In the left menu, go to **Networking** under **Settings**
   4. Click **Access restriction** under **Inbound Traffic**
   5. Under **Site access**, click the **X** to delete the "DenyAll" rule
   6. Click **Save**
   7. Wait ~30 seconds and refresh your browser
   
   > **⚠️ Security Warning**: This makes your app publicly accessible to the entire internet. Only use for quick testing and remember to re-enable restrictions immediately after.

5. **Insufficient quota**: Check your subscription quotas for the target region

6. **Name conflicts**: Resource names include a unique suffix to avoid conflicts  

### Viewing Application Logs

**Real-time log streaming** is the fastest way to troubleshoot application issues. It shows live logs from your App Service, including application errors, startup messages, and diagnostic information.

**Via Azure Portal:**

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your App Service
3. In the left menu, click **Log stream** under **Monitoring**
4. Wait a few seconds for the connection to establish
5. You'll see real-time logs from your application

**Via Azure CLI:**

```bash
# Stream logs in real-time
az webapp log tail --name <app-service-name> --resource-group <resource-group-name>

# Download recent logs
az webapp log download --name <app-service-name> --resource-group <resource-group-name>
```

**What to look for in logs:**

- **Database connection errors**: Look for messages containing "DB Init failed", "Connection was denied", or "Login failed"
- **Azure AD errors**: Look for "IDW10106", "ClientId", or authentication-related errors
- **Startup errors**: Check the first few lines after app restart for configuration issues
- **Application exceptions**: Stack traces and error messages from your .NET application

> **💡 Tip**: Keep the log stream open while making configuration changes (like adding environment variables or granting SQL permissions) to see the effects immediately. Restart the app and watch the logs to verify the changes worked.

---

### Useful Commands

```bash
# Check current environment values
azd env get-values

# Check deployment status
azd show

# View environment variables
azd env list

# Validate configuration before deploying
azd provision --preview

# Clean up resources
azd down

# Clean up resources forcefully
azd down --force --purge 

```

## Next Steps

After successful provisioning:
1. Deploy your application: `azd deploy`
2. Access your app via the Application Gateway public IP
3. Monitor via Application Insights and Log Analytics
