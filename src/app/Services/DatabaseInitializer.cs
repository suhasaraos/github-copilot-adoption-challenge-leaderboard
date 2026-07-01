using Microsoft.EntityFrameworkCore;
using System.Text;

namespace LeaderboardApp.Services
{
    public class DatabaseInitializer
    {
        private readonly InitialDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(InitialDbContext context, ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            var sqlScript = new StringBuilder();
            var provider = _context.Database.ProviderName;
            bool isPostgres = provider.Contains("PostgreSQL");
            bool isSqlServer = provider.Contains("SqlServer");

            void AppendCreateTable(string pgSql, string sqlServerSql)
            {
                sqlScript.AppendLine(isPostgres ? pgSql : sqlServerSql);
            }

            // 1. Teams
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS Teams (
                    TeamId UUID PRIMARY KEY,
                    Name VARCHAR NOT NULL,
                    Icon VARCHAR NOT NULL,
                    Tagline VARCHAR NOT NULL,
                    GitHubSlug VARCHAR NOT NULL,
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Teams')
                CREATE TABLE Teams (
                    TeamId UNIQUEIDENTIFIER PRIMARY KEY,
                    Name NVARCHAR(MAX) NOT NULL,
                    -- Icon NVARCHAR(MAX) NOT NULL,
                    Icon NVARCHAR(MAX) NULL,
                    Tagline NVARCHAR(MAX) NOT NULL,
                    -- GitHubSlug NVARCHAR(450) NOT NULL
                    GitHubSlug NVARCHAR(450) NULL
                );");

            // 2. Participants
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS Participants(
                    ParticipantId UUID PRIMARY KEY,
                    FirstName VARCHAR NOT NULL,
                    LastName VARCHAR NOT NULL,
                    NickName VARCHAR NOT NULL UNIQUE,
                    Email VARCHAR NOT NULL UNIQUE,
                    TeamId UUID REFERENCES Teams(TeamId) ON DELETE CASCADE,
                    ExternalId UUID NOT NULL,
                    GitHubHandle VARCHAR,
                    MsLearnHandle VARCHAR,
                    Passcode VARCHAR,
                    PasscodeExpiration TIMESTAMP WITH TIME ZONE,
                    RefreshToken VARCHAR,
                    LastLogin TIMESTAMP WITH TIME ZONE
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Participants')
                CREATE TABLE Participants(
                    ParticipantId UNIQUEIDENTIFIER PRIMARY KEY,
                    FirstName NVARCHAR(MAX) NOT NULL,
                    LastName NVARCHAR(MAX) NOT NULL,
                    NickName NVARCHAR(450) NOT NULL UNIQUE,
                    Email NVARCHAR(450) NOT NULL UNIQUE,
                    TeamId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Teams(TeamId) ON DELETE CASCADE,
                    ExternalId UNIQUEIDENTIFIER NOT NULL,
                    GitHubHandle NVARCHAR(450),
                    MsLearnHandle NVARCHAR(450),
                    Passcode NVARCHAR(MAX),
                    PasscodeExpiration DATETIME2,
                    RefreshToken NVARCHAR(MAX),
                    LastLogin DATETIME2
                );");

            // 3. LeaderboardEntries
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS LeaderboardEntries (
                    LeaderboardEntryId UUID PRIMARY KEY,
                    TeamId UUID REFERENCES Teams(TeamId) ON DELETE CASCADE,
                    TeamName VARCHAR NOT NULL,
                    Score INTEGER NOT NULL,
                    LastUpdated TIMESTAMP WITH TIME ZONE NOT NULL
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeaderboardEntries')
                CREATE TABLE LeaderboardEntries (
                    LeaderboardEntryId UNIQUEIDENTIFIER PRIMARY KEY,
                    TeamId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Teams(TeamId) ON DELETE CASCADE,
                    TeamName NVARCHAR(MAX) NOT NULL,
                    Score INT NOT NULL,
                    LastUpdated DATETIME2 NOT NULL
                );");

            // 4. Activities
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS Activities (
                    ActivityId SERIAL PRIMARY KEY,
                    Name VARCHAR(255) NOT NULL,
                    WeightType VARCHAR(50) NOT NULL CHECK (WeightType IN ('Fixed', 'Multiplier')),
                    Weight DECIMAL(10, 2) NOT NULL,
                    Scope VARCHAR(50) NOT NULL CHECK (Scope IN ('User', 'Team')),
                    Frequency VARCHAR(50) NOT NULL CHECK (Frequency IN ('Once', 'Daily', 'Weekly'))
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Activities')
                CREATE TABLE Activities (
                    ActivityId INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    WeightType NVARCHAR(50) NOT NULL CHECK (WeightType IN ('Fixed', 'Multiplier')),
                    Weight DECIMAL(10, 2) NOT NULL,
                    Scope NVARCHAR(50) NOT NULL CHECK (Scope IN ('User', 'Team')),
                    Frequency NVARCHAR(50) NOT NULL CHECK (Frequency IN ('Once', 'Daily', 'Weekly'))
                );");

            // 5. Challenges (Must be created before ParticipantScores due to FK reference)
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS Challenges (
                    ChallengeId INT IDENTITY(1,1) PRIMARY KEY,
                    Title VARCHAR(255) NOT NULL,
                    Content TEXT NOT NULL,
                    PostedDate TIMESTAMP WITH TIME ZONE,
                    ActivityId INT,
                    FOREIGN KEY (ActivityId) REFERENCES Activities(ActivityId)
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Challenges')
                CREATE TABLE Challenges (
                    ChallengeId INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(255) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    PostedDate DATETIME2,
                    ActivityId INT FOREIGN KEY REFERENCES Activities(ActivityId)
                );");

            // 6. ParticipantScores
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS ParticipantScores (
                    ScoreId SERIAL PRIMARY KEY,
                    ParticipantId UUID NOT NULL,
                    ActivityId INT NOT NULL,
                    ChallengeId INT NOT NULL,
                    TeamId UUID NOT NULL,
                    Score DECIMAL(10, 2) NOT NULL,
                    Timestamp TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    ValidationLink VARCHAR,
                    FOREIGN KEY (ParticipantId) REFERENCES Participants(ParticipantId) ON DELETE CASCADE,
                    FOREIGN KEY (ActivityId) REFERENCES Activities(ActivityId),
                    FOREIGN KEY (ChallengeId) REFERENCES Challenges(ChallengeId),
                    FOREIGN KEY (TeamId) REFERENCES Teams(TeamId)
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ParticipantScores')
                CREATE TABLE ParticipantScores (
                    ScoreId INT IDENTITY(1,1) PRIMARY KEY,
                    ParticipantId UNIQUEIDENTIFIER NOT NULL,
                    ActivityId INT NOT NULL,
                    ChallengeId INT NOT NULL,
                    TeamId UNIQUEIDENTIFIER NOT NULL,
                    Score DECIMAL(10, 2) NOT NULL,
                    Timestamp DATETIME2 DEFAULT SYSUTCDATETIME(),
                    ValidationLink NVARCHAR(MAX),
                    FOREIGN KEY (ParticipantId) REFERENCES Participants(ParticipantId) ON DELETE CASCADE,
                    FOREIGN KEY (ActivityId) REFERENCES Activities(ActivityId),
                    FOREIGN KEY (ChallengeId) REFERENCES Challenges(ChallengeId),
                    FOREIGN KEY (TeamId) REFERENCES Teams(TeamId)
                );");


            //// 7. TeamDailySummaries
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS TeamDailySummaries (
            //        SummaryId SERIAL PRIMARY KEY,
            //        TeamId UUID REFERENCES Teams(TeamId) ON DELETE CASCADE,
            //        Day DATE NOT NULL,
            //        TotalSuggestionsCount INTEGER NOT NULL,
            //        TotalAcceptancesCount INTEGER NOT NULL,
            //        TotalLinesSuggested INTEGER NOT NULL,
            //        TotalLinesAccepted INTEGER NOT NULL,
            //        TotalActiveUsers INTEGER NOT NULL,
            //        TotalChatAcceptances INTEGER NOT NULL,
            //        TotalChatTurns INTEGER NOT NULL,
            //        TotalActiveChatUsers INTEGER NOT NULL
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TeamDailySummaries')
            //    CREATE TABLE TeamDailySummaries (
            //        SummaryId INT IDENTITY(1,1) PRIMARY KEY,
            //        TeamId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Teams(TeamId) ON DELETE CASCADE,
            //        Day DATE NOT NULL,
            //        TotalSuggestionsCount INT NOT NULL,
            //        TotalAcceptancesCount INT NOT NULL,
            //        TotalLinesSuggested INT NOT NULL,
            //        TotalLinesAccepted INT NOT NULL,
            //        TotalActiveUsers INT NOT NULL,
            //        TotalChatAcceptances INT NOT NULL,
            //        TotalChatTurns INT NOT NULL,
            //        TotalActiveChatUsers INT NOT NULL
            //    );");

            //// 7. Surveys
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS Surveys (
            //        SurveyId UUID PRIMARY KEY,
            //        Title VARCHAR(255) NOT NULL,
            //        Description TEXT,
            //        CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
            //        IsActive BOOLEAN DEFAULT TRUE
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Surveys')
            //    CREATE TABLE Surveys (
            //        SurveyId UNIQUEIDENTIFIER PRIMARY KEY,
            //        Title NVARCHAR(255) NOT NULL,
            //        Description NVARCHAR(MAX),
            //        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
            //        IsActive BIT DEFAULT 1
            //    );");

            //// 8. SurveyQuestions
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS SurveyQuestions (
            //        QuestionId UUID PRIMARY KEY,
            //        SurveyId UUID REFERENCES Surveys(SurveyId) ON DELETE CASCADE,
            //        QuestionText TEXT NOT NULL,
            //        QuestionType VARCHAR(50) CHECK (QuestionType IN ('Text', 'MultipleChoice', 'Rating', 'Boolean')) NOT NULL,
            //        CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SurveyQuestions')
            //    CREATE TABLE SurveyQuestions (
            //        QuestionId UNIQUEIDENTIFIER PRIMARY KEY,
            //        SurveyId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Surveys(SurveyId) ON DELETE CASCADE,
            //        QuestionText NVARCHAR(MAX) NOT NULL,
            //        QuestionType NVARCHAR(50) CHECK (QuestionType IN ('Text', 'MultipleChoice', 'Rating', 'Boolean')) NOT NULL,
            //        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
            //    );");

            //// 9. SurveyOptions
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS SurveyOptions (
            //        OptionId UUID PRIMARY KEY,
            //        QuestionId UUID REFERENCES SurveyQuestions(QuestionId) ON DELETE CASCADE,
            //        OptionText VARCHAR(255) NOT NULL,
            //        CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SurveyOptions')
            //    CREATE TABLE SurveyOptions (
            //        OptionId UNIQUEIDENTIFIER PRIMARY KEY,
            //        QuestionId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES SurveyQuestions(QuestionId) ON DELETE CASCADE,
            //        OptionText NVARCHAR(255) NOT NULL,
            //        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
            //    );");

            //// 10. SurveyResponses
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS SurveyResponses (
            //        ResponseId UUID PRIMARY KEY,
            //        SurveyId UUID REFERENCES Surveys(SurveyId) ON DELETE CASCADE,
            //        ParticipantId UUID REFERENCES Participants(ParticipantId) ON DELETE CASCADE,
            //        SubmittedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SurveyResponses')
            //    CREATE TABLE SurveyResponses (
            //        ResponseId UNIQUEIDENTIFIER PRIMARY KEY,
            //        SurveyId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Surveys(SurveyId) ON DELETE CASCADE,
            //        ParticipantId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES Participants(ParticipantId) ON DELETE CASCADE,
            //        SubmittedAt DATETIME2 DEFAULT SYSUTCDATETIME()
            //    );");

            //// 11. ResponseAnswers
            //AppendCreateTable(
            //    @"CREATE TABLE IF NOT EXISTS ResponseAnswers (
            //        AnswerId UUID PRIMARY KEY,
            //        ResponseId UUID REFERENCES SurveyResponses(ResponseId) ON DELETE CASCADE,
            //        QuestionId UUID REFERENCES SurveyQuestions(QuestionId) ON DELETE CASCADE,
            //        AnswerText TEXT,
            //        CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            //    );",
            //    @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ResponseAnswers')
            //    CREATE TABLE ResponseAnswers (
            //        AnswerId UNIQUEIDENTIFIER PRIMARY KEY,
            //        ResponseId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES SurveyResponses(ResponseId) ON DELETE CASCADE,
            //        QuestionId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES SurveyQuestions(QuestionId) ON DELETE NO ACTION,
            //        AnswerText NVARCHAR(MAX),
            //        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
            //    );");


            // 12. MetricsData
            AppendCreateTable(
                @"CREATE TABLE IF NOT EXISTS MetricsData (
                    MetricsId INT IDENTITY(1,1) PRIMARY KEY,
                    Date DATE NOT NULL,
                    OrgName VARCHAR(255),
                    JsonResponse JSONB NOT NULL
                );",
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MetricsData')
                CREATE TABLE MetricsData (
                    MetricsId INT IDENTITY(1,1) PRIMARY KEY,
                    Date DATE NOT NULL,
                    OrgName NVARCHAR(255),
                    JsonResponse NVARCHAR(MAX) NOT NULL
                );");

            if (sqlScript.Length > 0)
            {
                var dbConnection = _context.Database.GetDbConnection();

                try
                {
                    await dbConnection.OpenAsync();
                    await _context.Database.ExecuteSqlRawAsync(sqlScript.ToString());
                    _logger.LogInformation("Tables created or validated successfully.");                    
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error executing SQL script: {ex.Message}");
                }
                finally
                {
                    await dbConnection.CloseAsync();
                }
            }
        }        
    }
}
