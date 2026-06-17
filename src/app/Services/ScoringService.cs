using LeaderboardApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LeaderboardApp.Services
{
    public class ScoringService
    {
        private readonly GhcacDbContext _context;
        private readonly ILogger<ScoringService> _logger;
        private readonly GitHubService _gitHubService;
        private readonly bool _githubEnabled;

        public ScoringService(
            ILogger<ScoringService> logger,
            GitHubService gitHubService,
            GhcacDbContext context,
            IConfiguration configuration)
        {
            _logger = logger;
            _gitHubService = gitHubService;
            _context = context;
            _githubEnabled = configuration.GetValue<bool>("GitHubSettings:Enabled", true);
        }

        public async Task<bool> InsertTeamGitHubScoresAsync(string teamSlug)
        {
            if (!_githubEnabled)
            {
                _logger.LogInformation("GitHub scoring skipped because GitHubSettings:Enabled=false");
                return true;
            }

            // Step 1: Store org-level metrics (for audit/dashboard)
            try
            {
                var orgReport = await _gitHubService.GetOrgCopilotMetricsAsync();
                if (orgReport != null)
                {
                    var reportDate = orgReport.GetDate();
                    var existingDate = await _context.MetricsData
                        .Where(md => md.Date.Date == reportDate.Date)
                        .AnyAsync();

                    if (!existingDate)
                    {
                        _context.MetricsData.Add(new MetricsData
                        {
                            Date = reportDate,
                            JsonResponse = JsonSerializer.Serialize(orgReport)
                        });
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while storing org metrics for team {TeamSlug}", teamSlug);
            }

            // Step 2: Score per-participant by matching GitHub handles
            try
            {
                var team = await _context.Teams.FirstOrDefaultAsync(t => t.GitHubSlug == teamSlug);
                if (team == null)
                {
                    _logger.LogWarning("No team found with slug {TeamSlug}", teamSlug);
                    return false;
                }

                // Get ALL participants in this team who have a GitHub handle
                var participants = await _context.Participants
                    .Where(p => p.Teamid == team.Teamid && p.Githubhandle != null && p.Githubhandle != "")
                    .ToListAsync();

                if (participants.Count == 0)
                {
                    _logger.LogWarning("No participants with GitHub handles in team {TeamSlug}", teamSlug);
                    return false;
                }

                // Build a lookup of GitHub handle → participant (case-insensitive)
                var handleToParticipant = participants
                    .ToDictionary(p => p.Githubhandle!.ToLowerInvariant(), p => p);

                // Fetch ALL user metrics for yesterday from the org-wide report
                var allUserMetrics = await _gitHubService.GetAllUserMetricsForDayAsync(teamSlug);
                if (allUserMetrics == null || allUserMetrics.Count == 0)
                {
                    _logger.LogWarning("No user metrics available for scoring team {TeamSlug}", teamSlug);
                    return false;
                }

                // Filter to users whose GitHub login matches a participant in this team
                var teamUserMetrics = allUserMetrics
                    .Where(u => u.UserLogin != null && handleToParticipant.ContainsKey(u.UserLogin.ToLowerInvariant()))
                    .ToList();

                if (teamUserMetrics.Count == 0)
                {
                    _logger.LogWarning("No matching user metrics found for team {TeamSlug}. Participants: [{Handles}], Metrics users sample: [{Sample}]",
                        teamSlug,
                        string.Join(", ", handleToParticipant.Keys.Take(5)),
                        string.Join(", ", allUserMetrics.Take(5).Select(u => u.UserLogin)));
                    return false;
                }

                _logger.LogInformation("Found {Count} matching user metrics for team {TeamSlug} (out of {Total} org users)",
                    teamUserMetrics.Count, teamSlug, allUserMetrics.Count);

                var activities = await _context.Activities.ToListAsync();
                var scoreEntries = new List<Participantscore>();

                // Process each user's metrics individually
                foreach (var userMetric in teamUserMetrics)
                {
                    var participant = handleToParticipant[userMetric.UserLogin!.ToLowerInvariant()];
                    var metricDate = userMetric.GetDate();

                    // Get existing scores for this participant to avoid duplicates
                    var existingScores = await _context.Participantscores
                        .Where(ps => ps.Participantid == participant.Participantid
                                  && ps.Teamid == team.Teamid
                                  && ps.Timestamp.HasValue
                                  && ps.Timestamp.Value.Date == metricDate.Date)
                        .ToListAsync();

                    void AddScore(string activityName, decimal? value)
                    {
                        if (value.HasValue && value.Value > 0)
                        {
                            var activity = activities.FirstOrDefault(a => a.Name == activityName);
                            if (activity == null) return;

                            if (existingScores.Any(es => es.Activityid == activity.Activityid))
                                return;

                            scoreEntries.Add(new Participantscore
                            {
                                Scoreid = 0,
                                Participantid = participant.Participantid,
                                Activityid = activity.Activityid,
                                Challengeid = 24,
                                Teamid = team.Teamid,
                                Score = string.Equals(activity.Weighttype, "multiplier", StringComparison.OrdinalIgnoreCase)
                                    ? value.Value * activity.Weight
                                    : activity.Weight,
                                Timestamp = metricDate,
                                Validationlink = null
                            });
                        }
                    }

                    // Score top-level counts
                    AddScore("ActiveUsersPerDay", 1); // This user was active
                    if (userMetric.UserInitiatedInteractionCount > 0)
                        AddScore("EngagedUsersPerDay", 1);

                    // Score feature-level metrics
                    if (userMetric.TotalsByFeature != null)
                    {
                        int totalCodeSuggestions = 0, totalLinesAccepted = 0;
                        int totalChats = 0, totalChatInsertions = 0;
                        int totalDotComChats = 0, totalPRSummaries = 0;

                        foreach (var feature in userMetric.TotalsByFeature)
                        {
                            switch (feature.Feature?.ToLowerInvariant())
                            {
                                case "code_completion":
                                    totalCodeSuggestions += feature.CodeGenerationActivityCount;
                                    totalLinesAccepted += feature.LocAddedSum;
                                    break;
                                case "chat_panel":
                                case "chat_panel_agent_mode":
                                case "chat_panel_custom_mode":
                                    totalChats += feature.UserInitiatedInteractionCount;
                                    totalChatInsertions += feature.CodeAcceptanceActivityCount;
                                    break;
                                case "copilot_cli":
                                    totalChatInsertions += feature.CodeAcceptanceActivityCount;
                                    break;
                                case "dotcom_chat":
                                    totalDotComChats += feature.UserInitiatedInteractionCount;
                                    break;
                                case "pull_request_summary":
                                case "pr_summary":
                                    totalPRSummaries += feature.CodeGenerationActivityCount;
                                    break;
                            }
                        }

                        AddScore("TotalCodeSuggestions", totalCodeSuggestions);
                        AddScore("TotalLinesAccepted", totalLinesAccepted);
                        AddScore("TotalChats", totalChats);
                        AddScore("TotalChatInsertions", totalChatInsertions);
                        AddScore("TotalDotComChats", totalDotComChats);
                        AddScore("TotalPRSummariesCreated", totalPRSummaries);
                    }
                }

                if (scoreEntries.Count == 0)
                {
                    _logger.LogInformation("No new GitHub scores to insert for team {TeamSlug}", teamSlug);
                    return false;
                }

                _context.Participantscores.AddRange(scoreEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Inserted {Count} GitHub score entries for team {TeamSlug}", scoreEntries.Count, teamSlug);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while inserting GitHub scores for team {TeamSlug}", teamSlug);
                return false;
            }
        }
    }
}
