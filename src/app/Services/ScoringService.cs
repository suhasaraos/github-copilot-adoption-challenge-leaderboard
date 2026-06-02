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

            try
            {
                var orgmetrics = await _gitHubService.GetOrgCopilotMetricsAsync();
                if (orgmetrics == null)
                {
                    _logger.LogWarning("No metrics returned for organization");
                    return false;
                }
                else
                {
                    _logger.LogInformation("Metrics returned for organization");
                    if (orgmetrics != null)
                    {
                        // Save metrics to the database if it doesn't exist for that date
                        var metricDates = orgmetrics.Select(m => m.Date.Date).ToList();
                        var existingDates = await _context.MetricsData
                            .Where(md => metricDates.Contains(md.Date.Date))
                            .Select(md => md.Date.Date)
                            .ToListAsync();

                        // Add only new dates
                        foreach (var metric in orgmetrics)
                        {
                            if (!existingDates.Contains(metric.Date.Date))
                            {
                                _context.MetricsData.Add(new MetricsData
                                {
                                    Date = metric.Date,
                                    JsonResponse = JsonSerializer.Serialize(metric)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while inserting GitHub scores for team {TeamSlug}", teamSlug);
                return false;
            }

            try
            {
                var metrics = await _gitHubService.GetCopilotMetricsAsync(teamSlug);
                if (metrics == null)
                {
                    _logger.LogWarning("No metrics returned for team {TeamSlug}", teamSlug);
                    return false;
                }

                var filteredMetrics = metrics
                    .Where(m => m.Date.Date >= _challengeStartDate.Date)
                    .ToList();

                if (filteredMetrics.Count == 0)
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

                foreach (var metric in filteredMetrics)
                {
                    if (metric.IdeChat?.Editors != null)
                    {
                        foreach (var editor in metric.IdeChat.Editors)
                        {
                            if (editor.Models != null)
                            {
                                foreach (var model in editor.Models)
                                {
                                    AddScore("TotalChats", metric.Date, model.TotalChats);
                                    AddScore("TotalChatInsertions", metric.Date, model.TotalChatInsertionEvents);
                                    AddScore("TotalChatCopyEvents", metric.Date, model.TotalChatCopyEvents);
                                }
                            }
                        }
                    }

                    if (metric.CodeCompletions?.Editors != null)
                    {
                        foreach (var editor in metric.CodeCompletions.Editors)
                        {
                            if (editor.Models != null)
                            {
                                foreach (var model in editor.Models)
                                {
                                    if (model.Languages != null)
                                    {
                                        foreach (var language in model.Languages)
                                        {
                                            AddScore("TotalCodeSuggestions", metric.Date, language.TotalCodeSuggestions);
                                            AddScore("TotalCodeAcceptances", metric.Date, language.TotalCodeAcceptances);
                                            AddScore("TotalLinesSuggested", metric.Date, language.TotalLinesSuggested);
                                            AddScore("TotalLinesAccepted", metric.Date, language.TotalLinesAccepted);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (metric.DotComChat?.Models != null)
                    {
                        foreach (var model in metric.DotComChat.Models)
                        {
                            AddScore("TotalDotComChats", metric.Date, model.TotalChats);
                        }
                    }

                    if (metric.PullRequests?.Repositories != null)
                    {
                        foreach (var repo in metric.PullRequests.Repositories)
                        {
                            if (repo.Models != null)
                            {
                                foreach (var model in repo.Models)
                                {
                                    AddScore("TotalPRSummariesCreated", metric.Date, model.TotalPRSummariesCreated);
                                }
                            }
                        }
                    }

                    AddScore("ActiveUsersPerDay", metric.Date, metric.TotalActiveUsers);
                    AddScore("EngagedUsersPerDay", metric.Date, metric.TotalEngagedUsers);
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
    }
}
