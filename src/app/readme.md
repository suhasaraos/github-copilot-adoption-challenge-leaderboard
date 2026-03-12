# Leaderboard App - Setup Guide

## Running Locally (No Docker)

The application now runs using the built-in .NET 10 runtime on Azure App Service. For local development you can simply use the .NET CLI:

```powershell
cd src/LeaderboardApp
dotnet restore
dotnet run
```

Swagger UI is available at the /swagger endpoint for the active profile.

## Package Manager Console Commands
Use the following commands in the Package Manager Console for database scaffolding and migration:
```bash	
# Scaffold the DbContext for PostgreSQL
Scaffold-DbContext "Host=localhost;Database=ghcac-db;Username=postgres;Password={}" Npgsql.EntityFrameworkCore.PostgreSQL -OutputDir Models -Force

# Update the database after changes to the model
Update-Database

# Add a new migration
Add-Migration InitialMigration

# Remove the last migration
Remove-Migration
```

If error and need to fix connection string in the DbContext (inside OnConfiguring method), update the .cs file as below or remove the OnConfiguring method completely:
```csharp
{
    if (!optionsBuilder.IsConfigured)
    {
        // The connection string should come from the application's configuration
        // optionsBuilder.UseNpgsql("Host=localhost;Database=ghcac-db;Username=postgres;Password=challenge");
    }
}
```

## Database Create Table Scripts
The following SQL scripts create the necessary tables for the leaderboard application:

```sql
-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.Teams
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Teams (
    TeamId        UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
    Name          NVARCHAR(255)       NOT NULL,
    Icon          NVARCHAR(255)       NOT NULL,
    Tagline       NVARCHAR(512)       NOT NULL,
    GitHubSlug    NVARCHAR(255)       NOT NULL,
    SelectedOrg   NVARCHAR(255)       NULL
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.Participants
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Participants (
    ParticipantId       UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
    FirstName           NVARCHAR(100)       NOT NULL,
    LastName            NVARCHAR(100)       NOT NULL,
    NickName            NVARCHAR(100)       NOT NULL,
    Email               NVARCHAR(255)       NOT NULL UNIQUE,
    TeamId              UNIQUEIDENTIFIER    NULL
        CONSTRAINT FK_Participants_Teams
        REFERENCES dbo.Teams(TeamId)
        ON DELETE CASCADE,
    ExternalId          UNIQUEIDENTIFIER    NOT NULL,
    GitHubHandle        NVARCHAR(100)       NULL,
    MsLearnHandle       NVARCHAR(100)       NULL,
    Passcode            NVARCHAR(100)       NULL,
    PasscodeExpiration  DATETIME2           NULL,
    RefreshToken        NVARCHAR(4000)      NULL,
    LastLogin           DATETIME2           NULL,
    SelectedOrg         NVARCHAR(255)       NULL
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.Activities
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Activities (
    ActivityId   INT                  NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(255)        NOT NULL,
    WeightType   NVARCHAR(50)         NOT NULL
                   CHECK (WeightType IN ('Fixed','Multiplier')),
    Weight       DECIMAL(10,2)        NOT NULL,
    Scope        NVARCHAR(50)         NOT NULL
                   CHECK (Scope IN ('Individual','Team')),
    Frequency    NVARCHAR(50)         NOT NULL
                   CHECK (Frequency IN ('Once','Daily','Weekly'))
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.Challenges
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Challenges (
    ChallengeId   INT                  NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title         NVARCHAR(255)        NOT NULL,
    Content       NVARCHAR(MAX)        NOT NULL,
    PostedDate    DATETIME2            NULL,
    ActivityId    INT                  NULL
         CONSTRAINT FK_Challenges_Activities
         REFERENCES dbo.Activities(ActivityId)
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.LeaderboardEntries
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.LeaderboardEntries (
    LeaderboardEntryId  UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
    TeamId              UNIQUEIDENTIFIER    NULL
         CONSTRAINT FK_LeaderboardEntries_Teams
         REFERENCES dbo.Teams(TeamId)
         ON DELETE CASCADE,
    TeamName            NVARCHAR(255)       NOT NULL,
    Score               INT                 NOT NULL,
    LastUpdated         DATETIME2           NOT NULL
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.MetricsData
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.MetricsData (
    MetricsId     INT                  NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Date          DATE                 NOT NULL,
    OrgName       NVARCHAR(255)        NULL,
    JsonResponse  NVARCHAR(MAX)        NOT NULL
);


-- ─────────────────────────────────────────────────────────────────────────────
-- dbo.ParticipantScores
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.ParticipantScores (
    ScoreId          INT                  NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ParticipantId    UNIQUEIDENTIFIER     NOT NULL
         CONSTRAINT FK_ParticipantScores_Participants
         REFERENCES dbo.Participants(ParticipantId)
         ON DELETE CASCADE,
    ActivityId       INT                  NOT NULL
         CONSTRAINT FK_ParticipantScores_Activities
         REFERENCES dbo.Activities(ActivityId),
    ChallengeId      INT                  NOT NULL
         CONSTRAINT FK_ParticipantScores_Challenges
         REFERENCES dbo.Challenges(ChallengeId),
    TeamId           UNIQUEIDENTIFIER     NULL
         CONSTRAINT FK_ParticipantScores_Teams
         REFERENCES dbo.Teams(TeamId),
    Score            DECIMAL(10,2)        NOT NULL,
    Timestamp        DATETIME2            NULL,
    ValidationLink   NVARCHAR(2000)       NULL
);

``` 

## Database Inserts
Use the following SQL scripts to insert test data into the database:


### Scoring System

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

INSERT INTO Challenges (Title, Content, PostedDate, ActivityId)
VALUES 
(
    'Introduction to GitHub Copilot',
    'Complete the <strong>Introduction to GitHub Copilot</strong> module (19 min / 7 Units). GitHub Copilot uses OpenAI Codex to suggest code and entire functions in real time, right from your editor. <a href="https://learn.microsoft.com/en-us/training/modules/introduction-to-github-copilot/" target="_blank">Start Learning</a>',
    SYSDATETIME(),
    10
),
(
    'GitHub Copilot Certification Challenge',
    'Validate your Copilot skills and earn your badge by completing the <a href="https://learn.microsoft.com/en-us/credentials/certifications/github-copilot-technical-skills/" target="_blank">GitHub Copilot Technical Skills Certification</a>.',
    SYSDATETIME(),
    11
),
(
    'Try a link - its beautiful',
    '<a href="#" onclick="clickLink(17, ''https://code.visualstudio.com/blogs/2025/02/12/next-edit-suggestions''); return false;"> Next Edit Suggestions (NES) </a>...',
    SYSDATETIME(),
    13
),
(
    'Getting Code Suggestions in SQL with Copilot',
    'Explore GitHub Copilot’s real-time SQL code suggestions inside Azure Data Studio. Try writing joins or describing your goal in comments to see Copilot suggest full queries! <br><br>Example: <pre>SELECT [UserId], [Red], [Orange], [Yellow], [Green], [Blue], [Purple], [Rainbow] FROM [Tag].[Scoreboard] INNER JOIN</pre> Copilot may suggest a join automatically. <br><br>Or try natural language comments for code generation.',
    SYSDATETIME(),
    14
);

```

### Sample Data

```sql
INSERT INTO Teams (TeamId, Name, Icon, Tagline)
VALUES 
    ('f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', 'Team Alpha', 'alpha-icon.png', 'Leading the way'),
    ('c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', 'Team Bravo', 'bravo-icon.png', 'Bravery in action');

INSERT INTO Participants (ParticipantId, FirstName, LastName, NickName, Email, TeamId, ExternalId, GitHubHandle, MsLearnHandle)
VALUES 
    ('8d84c1e5-3c6a-4c61-82b8-74ad5e9c1d34', 'John', 'Doe', 'Johnny', 'john.doe@example.com', 'f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', '67f1b972-d5c4-4efc-82a3-9a4a4b7e2c32', 'johnny_github', 'john_mslearn'),
    ('6a8c1c32-4f2b-4f5d-91b7-81ad7e9c2d45', 'Jane', 'Smith', 'Janie', 'jane.smith@example.com', 'c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', 'e1b7c46e-2a5b-4e3f-8f2a-9a6c9b5a1e13', 'janie_github', 'jane_mslearn');

INSERT INTO LeaderboardEntries (LeaderboardEntryId, TeamId, TeamName, Score, LastUpdated)
VALUES 
    ('b74a2c74-9f7d-4a5a-bd92-8491a3c1b8b4', 'f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', 'Team Alpha', 250, '2024-08-12 10:00:00'),
    ('e92c3a6e-4a3b-4c4f-bf8b-72b3e9c1f5c6', 'c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', 'Team Bravo', 200, '2024-08-12 11:00:00');

INSERT INTO ParticipantScores (ParticipantId, ActivityId, Score)
VALUES 
    ('8d84c1e5-3c6a-4c61-82b8-74ad5e9c1d34', 1, 100), -- John Doe used GitHub Copilot
    ('6a8c1c32-4f2b-4f5d-91b7-81ad7e9c2d45', 2, 150); -- Jane Smith completed a learning module with a score multiplier

INSERT INTO TeamDailySummaries (TeamId, Day, TotalSuggestionsCount, TotalAcceptancesCount, TotalLinesSuggested, TotalLinesAccepted, TotalActiveUsers, TotalChatAcceptances, TotalChatTurns, TotalActiveChatUsers) 
VALUES
('f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', CURRENT_DATE, 1000, 800, 1800, 1200, 10, 32, 200, 4),
('c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', CURRENT_DATE, 800, 600, 1100, 700, 12, 57, 426, 8),
('f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', CURRENT_DATE - INTERVAL '1 day', 1500, 1100, 2200, 1600, 15, 40, 300, 6),
('c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', CURRENT_DATE - INTERVAL '1 day', 1200, 900, 1500, 1000, 14, 60, 500, 10),
('f7b6a37c-6147-4f3d-8c6c-7b0e9c3c16a1', CURRENT_DATE - INTERVAL '2 days', 1300, 1000, 1900, 1400, 13, 45, 350, 7),
('c4a4a5e7-2a5d-4f3f-8e6c-7d8e9c3c17a2', CURRENT_DATE - INTERVAL '2 days', 1100, 800, 1300, 900, 11, 50, 400, 9);

```