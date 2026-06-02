using LeaderboardApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeaderboardApp.Services
{
    public class ScoringService
    {
        private readonly GhcacDbContext _context;
        private readonly ILogger<ScoringService> _logger;
        private readonly GitHubService _gitHubService;
        private readonly DateTime _challengeStartDate;
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

            var challengeStartDateString = configuration["ChallengeSettings:ChallengeStartDate"];
            if (!DateTime.TryParse(challengeStartDateString, out _challengeStartDate))
            {
                _challengeStartDate = DateTime.MinValue;
                _logger.LogWarning("ChallengeStartDate is missing or invalid in configuration. All scores will be processed.");
            }
        }

        public async Task<bool> InsertTeamGitHubScoresAsync(string teamSlug)
        {
            if (!_githubEnabled)
            {
                _logger.LogInformation("GitHub scoring skipped because GitHubSettings:Enabled=false");
                return true; // treat as success
            }

            // ============================================================
            // UPDATED: Now uses the new Copilot Usage Metrics API (v2026-03-10)
            // OLD: Called GetOrgCopilotMetricsAsync() which returned List<GitHubMetrics>
            //      with nested editors/models/languages structure
            // NEW: Calls GetOrgCopilotMetricsAsync(day) which returns CopilotOrgDayReport
            //      with flat totals_by_feature[] array
            // ============================================================

            // Step 1: Store org-level metrics (for audit/dashboard)
            try
            {
                var orgReport = await _gitHubService.GetOrgCopilotMetricsAsync();
                if (orgReport == null)
                {
                    _logger.LogWarning("No org metrics returned from new API");
                }
                else
                {
                    _logger.LogInformation("Org metrics returned: daily_active_users={ActiveUsers}", orgReport.DailyActiveUsers);
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
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while storing org metrics for team {TeamSlug}", teamSlug);
                // Don't return false - continue to team scoring
            }

            // Step 2: Get per-user metrics filtered by team and score them
            // ============================================================
            // OLD: Called GetCopilotMetricsAsync(teamSlug) → List<GitHubMetrics>
            //      Then iterated nested: metric.IdeChat.Editors[].Models[].TotalChats etc.
            // NEW: Calls GetCopilotMetricsAsync(teamSlug, day) → List<CopilotUserDayReport>
            //      Each user report has flat totals_by_feature[] with feature name + counts
            //      We SUM across all users in the team for each feature/activity
            // ============================================================
            try
            {
                var userMetrics = await _gitHubService.GetCopilotMetricsAsync(teamSlug);
                if (userMetrics == null || userMetrics.Count == 0)
                {
                    _logger.LogWarning("No user metrics returned for team {TeamSlug}", teamSlug);
                    return false;
                }

                // Group by day (new reports come one-per-user-per-day)
                var metricsByDay = userMetrics
                    .GroupBy(u => u.GetDate().Date)
                    .Where(g => g.Key >= _challengeStartDate.Date)
                    .ToList();

                if (metricsByDay.Count == 0)
                {
                    _logger.LogInformation("No metrics to process for team {TeamSlug} after ChallengeStartDate {ChallengeStartDate}", teamSlug, _challengeStartDate);
                    return false;
                }

                var team = await _context.Teams.FirstOrDefaultAsync(t => t.GitHubSlug == teamSlug);
                if (team == null)
                {
                    _logger.LogWarning("No team found with slug {TeamSlug}", teamSlug);
                    return false;
                }

                var activities = _context.Activities.ToList();

                var participant = await _context.Participants
                    .Where(p => p.Teamid == team.Teamid)
                    .FirstOrDefaultAsync();

                if (participant == null)
                {
                    return false;
                }

                var existingScores = await _context.Participantscores
                    .Where(ps => ps.Participantid == participant.Participantid && ps.Teamid == team.Teamid)
                    .ToListAsync();

                var scoreEntries = new List<Participantscore>();

                void AddScore(string activityName, DateTime metricDate, decimal? value)
                {
                    if (value.HasValue && value.Value > 0)
                    {
                        var activity = activities.FirstOrDefault(a => a.Name == activityName);

                        if (activity == null)
                        {
                            _logger.LogWarning("Activity {ActivityName} not found for team {TeamSlug}", activityName, teamSlug);
                            return;
                        }

                        if (existingScores.Any(es => es.Activityid == activity.Activityid && es.Timestamp.HasValue && es.Timestamp.Value.Date == metricDate.Date))
                        {
                            _logger.LogInformation("Duplicate score detected for activity {ActivityName} on {MetricDate} for team {TeamSlug}", activityName, metricDate, teamSlug);
                            return;
                        }

                        scoreEntries.Add(new Participantscore
                        {
                            Scoreid = 0,
                            Participantid = participant.Participantid,
                            Activityid = activity.Activityid,
                            Challengeid = 24,
                            Teamid = team.Teamid,
                            Score = string.Equals(activity.Weighttype, "multiplier", StringComparison.OrdinalIgnoreCase) ? value.Value * activity.Weight : activity.Weight,
                            Timestamp = metricDate,
                            Validationlink = null
                        });
                    }
                }

                // ============================================================
                // NEW SCORING LOGIC: Aggregate per-user metrics by day
                // The new API gives us totals_by_feature[] per user. We sum across 
                // all team users for each day to get team totals.
                //
                // Feature mapping (new API feature names → old activity names):
                //   "code_completion" → TotalCodeSuggestions (code_generation_activity_count)
                //   "code_completion" → TotalLinesAccepted (loc_added_sum)
                //   "chat_panel" + "chat_panel_agent_mode" + "chat_panel_custom_mode" → TotalChats (user_initiated_interaction_count)
                //   "copilot_cli" → TotalChatInsertions (code_acceptance_activity_count)
                //   "dotcom_chat" → TotalDotComChats (user_initiated_interaction_count)
                //   "pull_request_summary" → TotalPRSummariesCreated (code_generation_activity_count)
                //   top-level daily_active = count of users with any activity → ActiveUsersPerDay
                //   top-level engaged = count of users with interaction → EngagedUsersPerDay
                // ============================================================

                foreach (var dayGroup in metricsByDay)
                {
                    var metricDate = dayGroup.Key;
                    var usersForDay = dayGroup.ToList();

                    // ActiveUsersPerDay = number of users with any activity this day
                    AddScore("ActiveUsersPerDay", metricDate, usersForDay.Count);

                    // EngagedUsersPerDay = number of users who initiated interactions
                    var engagedUsers = usersForDay.Count(u => u.UserInitiatedInteractionCount > 0);
                    AddScore("EngagedUsersPerDay", metricDate, engagedUsers);

                    // Aggregate feature totals across all team users for this day
                    int totalCodeSuggestions = 0;
                    int totalLinesAccepted = 0;
                    int totalChats = 0;
                    int totalChatInsertions = 0;
                    int totalChatCopyEvents = 0; // Not directly available in new API, using code_acceptance from chat features
                    int totalDotComChats = 0;
                    int totalPRSummaries = 0;

                    foreach (var user in usersForDay)
                    {
                        if (user.TotalsByFeature == null) continue;

                        foreach (var feature in user.TotalsByFeature)
                        {
                            switch (feature.Feature?.ToLowerInvariant())
                            {
                                case "code_completion":
                                    // TotalCodeSuggestions = how many code suggestions were generated
                                    totalCodeSuggestions += feature.CodeGenerationActivityCount;
                                    // TotalLinesAccepted = lines of code actually added by user
                                    totalLinesAccepted += feature.LocAddedSum;
                                    break;

                                case "chat_panel":
                                case "chat_panel_agent_mode":
                                case "chat_panel_custom_mode":
                                    // TotalChats = user-initiated interactions in IDE chat
                                    totalChats += feature.UserInitiatedInteractionCount;
                                    // TotalChatInsertions = code accepted from chat
                                    totalChatInsertions += feature.CodeAcceptanceActivityCount;
                                    break;

                                case "copilot_cli":
                                    // CLI interactions also count as chat insertions when code is accepted
                                    totalChatInsertions += feature.CodeAcceptanceActivityCount;
                                    break;

                                case "dotcom_chat":
                                    // TotalDotComChats = interactions in github.com chat
                                    totalDotComChats += feature.UserInitiatedInteractionCount;
                                    break;

                                case "pull_request_summary":
                                case "pr_summary":
                                    // TotalPRSummariesCreated = PR summaries generated
                                    totalPRSummaries += feature.CodeGenerationActivityCount;
                                    break;
                            }
                        }
                    }

                    AddScore("TotalCodeSuggestions", metricDate, totalCodeSuggestions);
                    AddScore("TotalLinesAccepted", metricDate, totalLinesAccepted);
                    AddScore("TotalChats", metricDate, totalChats);
                    AddScore("TotalChatInsertions", metricDate, totalChatInsertions);
                    AddScore("TotalChatCopyEvents", metricDate, totalChatCopyEvents > 0 ? totalChatCopyEvents : (int?)null);
                    AddScore("TotalDotComChats", metricDate, totalDotComChats);
                    AddScore("TotalPRSummariesCreated", metricDate, totalPRSummaries);
                }

                if (scoreEntries.Count == 0)
                {
                    _logger.LogInformation("No valid GitHub scores to insert for team {TeamSlug}", teamSlug);
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

        // ============================================================
        // OLD SCORING LOGIC (kept for reference - used nested model iteration):
        // foreach (var metric in filteredMetrics)
        // {
        //     if (metric.IdeChat?.Editors != null)
        //         foreach (var editor in metric.IdeChat.Editors)
        //             if (editor.Models != null)
        //                 foreach (var model in editor.Models)
        //                 {
        //                     AddScore("TotalChats", metric.Date, model.TotalChats);
        //                     AddScore("TotalChatInsertions", metric.Date, model.TotalChatInsertionEvents);
        //                     AddScore("TotalChatCopyEvents", metric.Date, model.TotalChatCopyEvents);
        //                 }
        //
        //     if (metric.CodeCompletions?.Editors != null)
        //         foreach (var editor in metric.CodeCompletions.Editors)
        //             if (editor.Models != null)
        //                 foreach (var model in editor.Models)
        //                     if (model.Languages != null)
        //                         foreach (var language in model.Languages)
        //                         {
        //                             AddScore("TotalCodeSuggestions", metric.Date, language.TotalCodeSuggestions);
        //                             AddScore("TotalCodeAcceptances", metric.Date, language.TotalCodeAcceptances);
        //                             AddScore("TotalLinesSuggested", metric.Date, language.TotalLinesSuggested);
        //                             AddScore("TotalLinesAccepted", metric.Date, language.TotalLinesAccepted);
        //                         }
        //
        //     if (metric.DotComChat?.Models != null)
        //         foreach (var model in metric.DotComChat.Models)
        //             AddScore("TotalDotComChats", metric.Date, model.TotalChats);
        //
        //     if (metric.PullRequests?.Repositories != null)
        //         foreach (var repo in metric.PullRequests.Repositories)
        //             if (repo.Models != null)
        //                 foreach (var model in repo.Models)
        //                     AddScore("TotalPRSummariesCreated", metric.Date, model.TotalPRSummariesCreated);
        //
        //     AddScore("ActiveUsersPerDay", metric.Date, metric.TotalActiveUsers);
        //     AddScore("EngagedUsersPerDay", metric.Date, metric.TotalEngagedUsers);
        // }
        // ============================================================
    }
}
