# Build, Run & Azure SQL Setup

## Goal
Build and run the LeaderboardApp locally, and optionally configure Azure SQL Server firewall access so the app can connect to the database.

## Steps

### 1. Build the project

```powershell
cd src/app
dotnet build LeaderboardApp.csproj
```

Build should succeed. Warnings about nullable reference types (`CS8618`, `CS8602`, etc.) are expected and can be ignored.

### 2. Run the app

```powershell
cd src/app
dotnet run
```

The app starts on `http://localhost:5257`.  
If you see `DB Init failed: A task was canceled.` on startup, the Azure SQL firewall is blocking your IP — follow the steps below.

---

### 3. (Optional) Allow local machine to connect to Azure SQL

Only needed if the app cannot reach the database. Requires the Azure CLI (`az`) and that you are already logged in (`az login`).

#### 3a. Get your current public IP

```powershell
(Invoke-WebRequest -Uri "https://api.ipify.org").Content
```

#### 3b. Find the SQL Server name and resource group

```powershell
az sql server list \
  --query "[?fullyQualifiedDomainName=='<your-server>.database.windows.net'] | [0].{name:name, resourceGroup:resourceGroup}" \
  -o json
```

Replace `<your-server>` with the hostname from `appsettings.Development.json` (e.g. `susql`).

#### 3c. Enable public network access (if disabled)

```powershell
az sql server update \
  --resource-group "<resource-group>" \
  --name "<sql-server-name>" \
  --enable-public-network true
```

#### 3d. Add a firewall rule for your IP

```powershell
$ip = (Invoke-WebRequest -Uri "https://api.ipify.org").Content.Trim()
az sql server firewall-rule create \
  --resource-group "<resource-group>" \
  --server "<sql-server-name>" \
  --name "LocalDev-$(hostname)" \
  --start-ip-address $ip \
  --end-ip-address $ip
```

#### 3e. Restart the app

```powershell
cd src/app
dotnet run
```

On a successful DB connection you will see:
```
info: LeaderboardApp.Services.DatabaseInitializer[0]
      Tables created or validated successfully.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5257
```

---

## Configuration reference

| File | Purpose |
|------|---------|
| `src/app/appsettings.json` | Base configuration (empty connection strings, feature flags) |
| `src/app/appsettings.Development.json` | Local dev overrides — Azure AD, SQL connection string |

The `ConnectionStrings.SqlServer` value in `appsettings.Development.json` uses `Authentication=Active Directory Default`, so the Azure CLI logged-in identity (or `DefaultAzureCredential`) is used — no password needed.
