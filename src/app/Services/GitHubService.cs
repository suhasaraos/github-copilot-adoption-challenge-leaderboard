using LeaderboardApp.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeaderboardApp.Services
{
    public class GitHubService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitHubService> _logger;
        private static readonly string GHApiURLPrefix = "https://api.github.com/orgs";
        private readonly bool _enabled;

        // ============================================================
        // UPDATED: June 2026 - Migrated from deprecated Copilot Metrics API
        // OLD API: X-GitHub-Api-Version: 2022-11-28, endpoint: /orgs/{org}/copilot/metrics
        //   - Was shut down April 2, 2026 (returns 404)
        //   - See: https://github.blog/changelog/2026-01-29-closing-down-notice-of-legacy-copilot-metrics-apis/
        // NEW API: X-GitHub-Api-Version: 2026-03-10, endpoint: /orgs/{org}/copilot/metrics/reports/...
        //   - Returns download_links to NDJSON report files
        //   - Org-level: /organization-1-day?day=YYYY-MM-DD
        //   - Per-user: /users-1-day?day=YYYY-MM-DD (NDJSON with user_login)
        //   - User-teams: /user-teams-1-day?day=YYYY-MM-DD (user->team mapping)
        //   - Requires read:org scope for classic PATs (admin:org also works)
        // ============================================================

        public GitHubService(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _enabled = configuration.GetValue<bool>("GitHubSettings:Enabled", true);

            if (!_enabled)
            {
                _logger.LogWarning("GitHub integration disabled via configuration (GitHubSettings:Enabled=false). All GitHubService calls will be no-ops.");
                return; // Do not configure client
            }

            // Configure default headers only once
            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                var token = _configuration["GitHubSettings:PAT"];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/vnd.github+json"))
            {
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            }

            if (!_httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
            {
                // UPDATED: Changed from "2022-11-28" to "2026-03-10" for new Copilot Usage Metrics API
                _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
            }

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LeaderboardApp");
            }
        }

        private bool IsDisabled()
        {
            if (_enabled) return false;
            _logger.LogDebug("GitHubService call skipped because integration is disabled.");
            return true;
        }

        // ============================================================
        // OLD API METHOD (DEPRECATED - shut down April 2, 2026):
        // Used endpoint: GET /orgs/{org}/copilot/metrics
        // Returned: List<GitHubMetrics> with nested editors/models/languages
        // ============================================================
        // public async Task<List<GitHubMetrics>?> GetOrgCopilotMetricsAsync()
        // {
        //     if (IsDisabled()) return new List<GitHubMetrics>();
        //     var org = _configuration["GitHubSettings:Org"];
        //     if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return null; }
        //     var url = $"{GHApiURLPrefix}/{org}/copilot/metrics";
        //     try
        //     {
        //         var response = await _httpClient.GetAsync(url);
        //         var jsonResponse = await response.Content.ReadAsStringAsync();
        //         if (!response.IsSuccessStatusCode) { _logger.LogError("Failed. Status: {StatusCode}", response.StatusCode); return null; }
        //         var metrics = JsonSerializer.Deserialize<List<GitHubMetrics>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        //         return metrics;
        //     }
        //     catch (Exception ex) { _logger.LogError(ex, "Error fetching organization Copilot metrics."); return null; }
        // }

        // ============================================================
        // NEW API METHOD: Get org-level metrics for a specific day
        // Endpoint: GET /orgs/{org}/copilot/metrics/reports/organization-1-day?day=YYYY-MM-DD
        // Returns: { download_links: [...], report_day: "..." }
        // Then downloads the NDJSON file from download_links[0]
        // ============================================================
        public async Task<CopilotOrgDayReport?> GetOrgCopilotMetricsAsync(DateTime? day = null)
        {
            if (IsDisabled()) return null;

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org))
            {
                _logger.LogWarning("GitHub organization is not configured.");
                return null;
            }

            var targetDay = (day ?? DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");
            var url = $"{GHApiURLPrefix}/{org}/copilot/metrics/reports/organization-1-day?day={targetDay}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch org metrics report links. Status: {StatusCode}, Response: {Response}", response.StatusCode, jsonResponse);
                    return null;
                }

                var reportLinks = JsonSerializer.Deserialize<CopilotReportLinks>(jsonResponse);
                if (reportLinks?.DownloadLinks == null || reportLinks.DownloadLinks.Count == 0)
                {
                    _logger.LogWarning("No download links returned for org metrics on {Day}", targetDay);
                    return null;
                }

                // Download the actual report (NDJSON - single line JSON for org report)
                var reportResponse = await _httpClient.GetAsync(reportLinks.DownloadLinks[0]);
                if (!reportResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download org report. Status: {StatusCode}", reportResponse.StatusCode);
                    return null;
                }

                var reportJson = await reportResponse.Content.ReadAsStringAsync();
                var report = JsonSerializer.Deserialize<CopilotOrgDayReport>(reportJson.Trim());

                _logger.LogInformation("Successfully fetched NEW org Copilot metrics for {Day}: daily_active_users={ActiveUsers}", targetDay, report?.DailyActiveUsers);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching org Copilot metrics for {Day}.", day);
                return null;
            }
        }

        public async Task<string> CreateTeamAsync(string teamName)
        {
            if (IsDisabled()) return string.Empty;

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org))
            {
                _logger.LogWarning("GitHub organization is not configured.");
                return string.Empty;
            }

            var githubPayload = new
            {
                name = teamName,
                description = "Auto-created from Copilot Challenge app",
                permission = "push",
                notification_setting = "notifications_enabled",
                privacy = "closed"
            };

            var response = await _httpClient.PostAsync(
                $"{GHApiURLPrefix}/{org}/teams",
                new StringContent(JsonSerializer.Serialize(githubPayload), Encoding.UTF8, "application/json")
            );

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GitHub team creation failed. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, jsonResponse);                
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var slug = doc.RootElement.GetProperty("slug").GetString();
                _logger.LogInformation("GitHub team created successfully. Slug: {Slug}", slug);

                // Remove any users in the newly created team
                await RemoveAllUsersFromTeamAsync(slug);
                _logger.LogInformation("Removed all users from newly created team {Slug}", slug);

                return slug ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse slug from GitHub response: {Response}", jsonResponse);
                return string.Empty;
            }
        }


        public async Task<string?> GetLastUserActivity(string? githubHandle)
        {
            if (IsDisabled()) return null;

            if (string.IsNullOrWhiteSpace(githubHandle))
            {
                _logger.LogWarning("GitHub handle is null or empty.");
                return null;
            }

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org))
            {
                _logger.LogWarning("GitHub organization is not configured.");
                return null;
            }

            try
            {
                var url = $"{GHApiURLPrefix}/{org}/members/{githubHandle}/copilot";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch last user activity. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorResponse);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var activityData = JsonSerializer.Deserialize<GitHubActivityResponse>(jsonResponse);

                if (activityData?.LastActivityAt != null)
                {
                    _logger.LogInformation("Last activity for user {GitHubHandle}: {LastActivityAt}", githubHandle, activityData.LastActivityAt);
                    return activityData.LastActivityAt;
                }

                _logger.LogWarning("No last activity found for user {GitHubHandle}.", githubHandle);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching last user activity for {GitHubHandle}.", githubHandle);
                return null;
            }
        }

        public async Task<bool> MoveUserToTeamAsync(string githubUsername, string? oldTeamSlug, string newTeamSlug)
        {
            if (IsDisabled()) return true; // treat as success to not block flows

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return false; }
            try
            {
                if (!string.IsNullOrWhiteSpace(oldTeamSlug))
                {
                    var removeUrl = $"{GHApiURLPrefix}/{org}/teams/{oldTeamSlug}/memberships/{githubUsername}";
                    _logger.LogInformation("Removing user {GitHubUsername} from team {TeamSlug} via URL: {Url}",
                        githubUsername, oldTeamSlug, removeUrl);

                    var removeResponse = await _httpClient.DeleteAsync(removeUrl);

                    if (!removeResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await removeResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Failed to remove user {GitHubUsername} from old team {OldTeamSlug}. Status: {StatusCode}, Response: {Response}",
                            githubUsername, oldTeamSlug, removeResponse.StatusCode, errorContent);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully removed user {GitHubUsername} from old team {TeamSlug}",
                            githubUsername, oldTeamSlug);
                    }
                }

                var addUrl = $"{GHApiURLPrefix}/{org}/teams/{newTeamSlug}/memberships/{githubUsername}";
                _logger.LogInformation("Adding user {GitHubUsername} to team {TeamSlug} via URL: {Url}",
                    githubUsername, newTeamSlug, addUrl);

                var addResponse = await _httpClient.PutAsync(addUrl, null);

                if (!addResponse.IsSuccessStatusCode)
                {
                    var errorResponse = await addResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to add user {GitHubUsername} to new team {NewTeamSlug}. Status: {StatusCode}, Response: {Response}",
                        githubUsername, newTeamSlug, addResponse.StatusCode, errorResponse);
                    return false;
                }

                _logger.LogInformation("Successfully moved user {GitHubUsername} to team {NewTeamSlug}.", githubUsername, newTeamSlug);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in MoveUserToTeamAsync for user {GitHubUsername} to team {TeamSlug}",
                    githubUsername, newTeamSlug);
                return false;
            }
        }

        public async Task<bool> RemoveAllUsersFromTeamAsync(string teamSlug)
        {
            if (IsDisabled()) return true; // nothing to do

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return false; }
            try
            {
                var membersUrl = $"{GHApiURLPrefix}/{org}/teams/{teamSlug}/members";
                var response = await _httpClient.GetAsync(membersUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch team members for team {TeamSlug}. Status: {StatusCode}, Response: {Response}",
                        teamSlug, response.StatusCode, errorResponse);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var members = JsonSerializer.Deserialize<List<GitHubMember>>(jsonResponse);

                if (members == null || !members.Any()) { _logger.LogInformation("No members found in team {TeamSlug}.", teamSlug); return true; }

                foreach (var member in members)
                {
                    var removeUrl = $"{GHApiURLPrefix}/{org}/teams/{teamSlug}/memberships/{member.Login}";
                    var removeResponse = await _httpClient.DeleteAsync(removeUrl);

                    if (!removeResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to remove user {GitHubUsername} from team {TeamSlug}. Status: {StatusCode}",
                            member.Login, teamSlug, removeResponse.StatusCode);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully removed user {GitHubUsername} from team {TeamSlug}.", member.Login, teamSlug);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while removing all users from team {TeamSlug}.", teamSlug);
                return false;
            }
        }

        public async Task<bool> RemoveUserFromTeamAsync(string teamSlug, string githubUsername)
        {
            if (IsDisabled()) return true;

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return false; }
            try
            {
                var removeUrl = $"{GHApiURLPrefix}/{org}/teams/{teamSlug}/memberships/{githubUsername}";
                _logger.LogInformation("Removing user {GitHubUsername} from team {TeamSlug} via URL: {Url}",
                    githubUsername, teamSlug, removeUrl);

                var removeResponse = await _httpClient.DeleteAsync(removeUrl);

                if (!removeResponse.IsSuccessStatusCode)
                {
                    var errorContent = await removeResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to remove user {GitHubUsername} from team {TeamSlug}. Status: {StatusCode}, Response: {Response}",
                        githubUsername, teamSlug, removeResponse.StatusCode, errorContent);
                    return false;
                }
                else
                {
                    _logger.LogInformation("Successfully removed user {GitHubUsername} from team {TeamSlug}",
                        githubUsername, teamSlug);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in RemoveUserFromTeamAsync for user {GitHubUsername} from team {TeamSlug}",
                    githubUsername, teamSlug);
                return false;
            }
        }

        // ============================================================
        // OLD API METHOD (DEPRECATED - shut down April 2, 2026):
        // Used endpoint: GET /orgs/{org}/team/{teamSlug}/copilot/metrics
        // Returned: List<GitHubMetrics> per team with nested editors/models/languages
        // ============================================================
        // public async Task<List<GitHubMetrics>?> GetCopilotMetricsAsync(string teamSlug)
        // {
        //     if (IsDisabled()) return new List<GitHubMetrics>();
        //     var org = _configuration["GitHubSettings:Org"];
        //     if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return null; }
        //     var url = $"{GHApiURLPrefix}/{org}/team/{teamSlug}/copilot/metrics";
        //     try
        //     {
        //         var response = await _httpClient.GetAsync(url);
        //         var jsonResponse = await response.Content.ReadAsStringAsync();
        //         if (!response.IsSuccessStatusCode) { _logger.LogError("Failed. Status: {StatusCode}", response.StatusCode); return null; }
        //         var metrics = JsonSerializer.Deserialize<List<GitHubMetrics>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        //         return metrics;
        //     }
        //     catch (Exception ex) { _logger.LogError(ex, "Error fetching GitHub Copilot metrics."); return null; }
        // }

        // ============================================================
        // NEW API METHOD: Get per-user metrics for a specific day
        // Endpoint: GET /orgs/{org}/copilot/metrics/reports/users-1-day?day=YYYY-MM-DD
        // Returns NDJSON: one JSON line per user with user_login, totals_by_feature, etc.
        // We filter to users in the team using the user-teams report
        // ============================================================
        public async Task<List<CopilotUserDayReport>?> GetCopilotMetricsAsync(string teamSlug, DateTime? day = null)
        {
            if (IsDisabled()) return new List<CopilotUserDayReport>();

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return null; }

            var targetDay = (day ?? DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");

            try
            {
                // Step 1: Get user-teams mapping to find which users belong to this team
                var teamUsers = await GetTeamUsersFromReportAsync(org, teamSlug, targetDay);
                if (teamUsers == null || teamUsers.Count == 0)
                {
                    _logger.LogWarning("No users found in team {TeamSlug} from user-teams report for {Day}. Falling back to team members API.", teamSlug, targetDay);
                    // Fallback: use the team members API
                    var members = await GetTeamMembersAsync(teamSlug);
                    teamUsers = members?.Select(m => m.Login.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
                }

                if (teamUsers.Count == 0)
                {
                    _logger.LogWarning("No users found in team {TeamSlug}", teamSlug);
                    return new List<CopilotUserDayReport>();
                }

                // Step 2: Get all users' metrics for the day
                var allUserReports = await GetAllUserMetricsForDayAsync(org, targetDay);
                if (allUserReports == null || allUserReports.Count == 0)
                {
                    _logger.LogWarning("No user metrics available for {Day}", targetDay);
                    return new List<CopilotUserDayReport>();
                }

                // Step 3: Filter to users in this team
                var teamMetrics = allUserReports
                    .Where(u => teamUsers.Contains(u.UserLogin?.ToLowerInvariant() ?? ""))
                    .ToList();

                _logger.LogInformation("Found {Count} user metrics for team {TeamSlug} on {Day} (out of {Total} total users)",
                    teamMetrics.Count, teamSlug, targetDay, allUserReports.Count);

                return teamMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Copilot metrics for team {TeamSlug}.", teamSlug);
                return null;
            }
        }

        // ============================================================
        // NEW HELPER: Get user-team mapping from the new API
        // Endpoint: GET /orgs/{org}/copilot/metrics/reports/user-teams-1-day?day=YYYY-MM-DD
        // Returns NDJSON: {"user_login":"...", "slug":"team-slug", ...}
        // ============================================================
        private async Task<HashSet<string>> GetTeamUsersFromReportAsync(string org, string teamSlug, string day)
        {
            var url = $"{GHApiURLPrefix}/{org}/copilot/metrics/reports/user-teams-1-day?day={day}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch user-teams report. Status: {StatusCode}", response.StatusCode);
                    return new HashSet<string>();
                }

                var reportLinks = JsonSerializer.Deserialize<CopilotReportLinks>(jsonResponse);
                if (reportLinks?.DownloadLinks == null || reportLinks.DownloadLinks.Count == 0)
                    return new HashSet<string>();

                var reportResponse = await _httpClient.GetAsync(reportLinks.DownloadLinks[0]);
                if (!reportResponse.IsSuccessStatusCode)
                    return new HashSet<string>();

                var reportText = await reportResponse.Content.ReadAsStringAsync();
                var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in reportText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var entry = JsonSerializer.Deserialize<CopilotUserTeamEntry>(line);
                    if (entry != null && string.Equals(entry.Slug, teamSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        users.Add(entry.UserLogin?.ToLowerInvariant() ?? "");
                    }
                }

                _logger.LogInformation("User-teams report: found {Count} users in team {TeamSlug} for {Day}", users.Count, teamSlug, day);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching user-teams report for {Day}", day);
                return new HashSet<string>();
            }
        }

        // ============================================================
        // NEW HELPER: Get ALL user metrics for a day (NDJSON)
        // Endpoint: GET /orgs/{org}/copilot/metrics/reports/users-1-day?day=YYYY-MM-DD
        // Returns NDJSON: one JSON per line per user
        // ============================================================
        private async Task<List<CopilotUserDayReport>> GetAllUserMetricsForDayAsync(string org, string day)
        {
            var url = $"{GHApiURLPrefix}/{org}/copilot/metrics/reports/users-1-day?day={day}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch users-1-day report. Status: {StatusCode}, Response: {Response}", response.StatusCode, jsonResponse);
                    return new List<CopilotUserDayReport>();
                }

                var reportLinks = JsonSerializer.Deserialize<CopilotReportLinks>(jsonResponse);
                if (reportLinks?.DownloadLinks == null || reportLinks.DownloadLinks.Count == 0)
                    return new List<CopilotUserDayReport>();

                var reports = new List<CopilotUserDayReport>();

                // May have multiple download links for large orgs
                foreach (var downloadLink in reportLinks.DownloadLinks)
                {
                    var reportResponse = await _httpClient.GetAsync(downloadLink);
                    if (!reportResponse.IsSuccessStatusCode) continue;

                    var reportText = await reportResponse.Content.ReadAsStringAsync();
                    foreach (var line in reportText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var userReport = JsonSerializer.Deserialize<CopilotUserDayReport>(line);
                        if (userReport != null)
                            reports.Add(userReport);
                    }
                }

                _logger.LogInformation("Fetched {Count} user reports for {Day}", reports.Count, day);
                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user metrics for {Day}", day);
                return new List<CopilotUserDayReport>();
            }
        }

        // ============================================================
        // NEW HELPER: Get org metrics for multiple days (for scoring service)
        // Iterates from startDate to yesterday, fetching org-level 1-day reports
        // ============================================================
        public async Task<List<CopilotOrgDayReport>> GetOrgMetricsForDateRangeAsync(DateTime startDate)
        {
            if (IsDisabled()) return new List<CopilotOrgDayReport>();

            var results = new List<CopilotOrgDayReport>();
            var endDate = DateTime.UtcNow.Date.AddDays(-1); // yesterday

            for (var date = startDate.Date; date <= endDate; date = date.AddDays(1))
            {
                var report = await GetOrgCopilotMetricsAsync(date);
                if (report != null)
                    results.Add(report);
            }

            return results;
        }

        // ============================================================
        // NEW HELPER: Get per-user metrics for multiple days (for team scoring)
        // ============================================================
        public async Task<List<CopilotUserDayReport>> GetTeamMetricsForDateRangeAsync(string teamSlug, DateTime startDate)
        {
            if (IsDisabled()) return new List<CopilotUserDayReport>();

            var results = new List<CopilotUserDayReport>();
            var endDate = DateTime.UtcNow.Date.AddDays(-1);

            for (var date = startDate.Date; date <= endDate; date = date.AddDays(1))
            {
                var dayMetrics = await GetCopilotMetricsAsync(teamSlug, date);
                if (dayMetrics != null)
                    results.AddRange(dayMetrics);
            }

            return results;
        }

        public async Task<List<GitHubMember>?> GetTeamMembersAsync(string? teamSlug)
        {
            if (IsDisabled()) return new List<GitHubMember>();

            if (string.IsNullOrWhiteSpace(teamSlug)) { _logger.LogWarning("Team slug is null or empty."); return null; }
            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return null; }
            try
            {
                var membersUrl = $"{GHApiURLPrefix}/{org}/teams/{teamSlug}/members";
                _logger.LogInformation("Fetching team members from URL: {Url}", membersUrl);
                var response = await _httpClient.GetAsync(membersUrl);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch team members for team {TeamSlug}. Status: {StatusCode}, Response: {Response}", teamSlug, response.StatusCode, jsonResponse);
                    return null;
                }
                var members = JsonSerializer.Deserialize<List<GitHubMember>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.LogInformation("Successfully retrieved {Count} members for team {TeamSlug}", members?.Count ?? 0, teamSlug);
                return members;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching members for team {TeamSlug}.", teamSlug);
                return null;
            }
        }

        public async Task<bool> DeleteTeamAsync(string teamSlug)
        {
            if (IsDisabled()) return true;

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return false; }

            try
            {
                var url = $"{GHApiURLPrefix}/{org}/teams/{teamSlug}";
                var response = await _httpClient.DeleteAsync(url);

                // 204 = deleted, 404 = already gone - both are fine
                if (!response.IsSuccessStatusCode &&
                    response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to delete GitHub team '{TeamSlug}'. Status: {StatusCode}, Response: {Response}",
                        teamSlug, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Successfully deleted GitHub team '{TeamSlug}'", teamSlug);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting GitHub team '{TeamSlug}'", teamSlug);
                return false;
            }
        }

        public async Task<List<GitHubTeam>?> GetAllTeamsAsync()
        {
            if (IsDisabled()) return new List<GitHubTeam>();

            var org = _configuration["GitHubSettings:Org"];
            if (string.IsNullOrWhiteSpace(org)) { _logger.LogWarning("GitHub organization is not configured."); return null; }

            try
            {
                var url = $"{GHApiURLPrefix}/{org}/teams?per_page=100";
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to list GitHub teams. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, jsonResponse);
                    return null;
                }

                var teams = JsonSerializer.Deserialize<List<GitHubTeam>>(jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation("Retrieved {Count} GitHub teams for org '{Org}'", teams?.Count ?? 0, org);
                return teams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GitHub teams for org '{Org}'", org);
                return null;
            }
        }

        // Placeholder for GitHubActivityResponse used above (if not already defined elsewhere)
        private class GitHubActivityResponse
        {
            [JsonPropertyName("last_activity_at")] public string? LastActivityAt { get; set; }
        }

        public class GitHubMember { [JsonPropertyName("login")] public string Login { get; set; } = string.Empty; }

        public class GitHubTeam
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
        }
    }
}