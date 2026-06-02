using System.Text.Json.Serialization;

namespace LeaderboardApp.Models
{
    // ============================================================
    // OLD MODEL (used with deprecated /copilot/metrics API - shut down April 2, 2026)
    // Kept for reference. The old API returned: List<GitHubMetrics>
    // where each entry had nested editors > models > languages
    // ============================================================
    // public class GitHubMetrics
    // {
    //     [JsonPropertyName("date")] public DateTime Date { get; set; }
    //     [JsonPropertyName("total_active_users")] public int TotalActiveUsers { get; set; }
    //     [JsonPropertyName("total_engaged_users")] public int TotalEngagedUsers { get; set; }
    //     [JsonPropertyName("copilot_ide_code_completions")] public GitHubCodeCompletions? CodeCompletions { get; set; }
    //     [JsonPropertyName("copilot_ide_chat")] public GitHubIdeChat? IdeChat { get; set; }
    //     [JsonPropertyName("copilot_dotcom_chat")] public GitHubDotComChat? DotComChat { get; set; }
    //     [JsonPropertyName("copilot_dotcom_pull_requests")] public GitHubPullRequests? PullRequests { get; set; }
    // }

    // ============================================================
    // NEW MODELS (used with Copilot Usage Metrics API v2026-03-10)
    // The new API uses a 2-step approach:
    //   1. Call /orgs/{org}/copilot/metrics/reports/... -> returns download_links
    //   2. Download NDJSON from those URLs
    // ============================================================

    /// <summary>
    /// Response from the initial API call containing signed download URLs
    /// </summary>
    public class CopilotReportLinks
    {
        [JsonPropertyName("download_links")]
        public List<string>? DownloadLinks { get; set; }

        [JsonPropertyName("report_day")]
        public string? ReportDay { get; set; }

        [JsonPropertyName("report_start_day")]
        public string? ReportStartDay { get; set; }

        [JsonPropertyName("report_end_day")]
        public string? ReportEndDay { get; set; }
    }

    /// <summary>
    /// Organization-level daily report (from /organization-1-day)
    /// Contains aggregate metrics for the entire org for one day
    /// </summary>
    public class CopilotOrgDayReport
    {
        [JsonPropertyName("day")]
        public string? Day { get; set; }

        [JsonPropertyName("organization_id")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("daily_active_users")]
        public int DailyActiveUsers { get; set; }

        [JsonPropertyName("weekly_active_users")]
        public int WeeklyActiveUsers { get; set; }

        [JsonPropertyName("monthly_active_users")]
        public int MonthlyActiveUsers { get; set; }

        [JsonPropertyName("monthly_active_chat_users")]
        public int MonthlyActiveChatUsers { get; set; }

        [JsonPropertyName("user_initiated_interaction_count")]
        public int UserInitiatedInteractionCount { get; set; }

        [JsonPropertyName("code_generation_activity_count")]
        public int CodeGenerationActivityCount { get; set; }

        [JsonPropertyName("code_acceptance_activity_count")]
        public int CodeAcceptanceActivityCount { get; set; }

        [JsonPropertyName("totals_by_feature")]
        public List<CopilotFeatureTotal>? TotalsByFeature { get; set; }

        [JsonPropertyName("totals_by_ide")]
        public List<CopilotIdeTotal>? TotalsByIde { get; set; }

        /// <summary>Helper to parse the Day string into DateTime</summary>
        public DateTime GetDate() => DateTime.TryParse(Day, out var d) ? d : DateTime.MinValue;
    }

    /// <summary>
    /// Per-user daily report (from /users-1-day NDJSON)
    /// One record per user per day with their individual metrics
    /// </summary>
    public class CopilotUserDayReport
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("user_login")]
        public string? UserLogin { get; set; }

        [JsonPropertyName("day")]
        public string? Day { get; set; }

        [JsonPropertyName("organization_id")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("user_initiated_interaction_count")]
        public int UserInitiatedInteractionCount { get; set; }

        [JsonPropertyName("code_generation_activity_count")]
        public int CodeGenerationActivityCount { get; set; }

        [JsonPropertyName("code_acceptance_activity_count")]
        public int CodeAcceptanceActivityCount { get; set; }

        [JsonPropertyName("totals_by_feature")]
        public List<CopilotFeatureTotal>? TotalsByFeature { get; set; }

        [JsonPropertyName("totals_by_ide")]
        public List<CopilotIdeTotal>? TotalsByIde { get; set; }

        /// <summary>Helper to parse the Day string into DateTime</summary>
        public DateTime GetDate() => DateTime.TryParse(Day, out var d) ? d : DateTime.MinValue;
    }

    /// <summary>
    /// User-team mapping entry (from /user-teams-1-day NDJSON)
    /// One record per user-team pair
    /// </summary>
    public class CopilotUserTeamEntry
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("user_login")]
        public string? UserLogin { get; set; }

        [JsonPropertyName("day")]
        public string? Day { get; set; }

        [JsonPropertyName("organization_id")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("team_id")]
        public long TeamId { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }
    }

    /// <summary>
    /// Feature-level totals (appears in both org and user reports)
    /// Features include: code_completion, chat_panel_agent_mode, copilot_cli,
    /// chat_panel, chat_panel_custom_mode, dotcom_chat, pull_request_summary, etc.
    /// </summary>
    public class CopilotFeatureTotal
    {
        [JsonPropertyName("feature")]
        public string? Feature { get; set; }

        [JsonPropertyName("user_initiated_interaction_count")]
        public int UserInitiatedInteractionCount { get; set; }

        [JsonPropertyName("code_generation_activity_count")]
        public int CodeGenerationActivityCount { get; set; }

        [JsonPropertyName("code_acceptance_activity_count")]
        public int CodeAcceptanceActivityCount { get; set; }

        [JsonPropertyName("loc_suggested_to_add_sum")]
        public int LocSuggestedToAddSum { get; set; }

        [JsonPropertyName("loc_suggested_to_delete_sum")]
        public int LocSuggestedToDeleteSum { get; set; }

        [JsonPropertyName("loc_added_sum")]
        public int LocAddedSum { get; set; }

        [JsonPropertyName("loc_deleted_sum")]
        public int LocDeletedSum { get; set; }
    }

    /// <summary>
    /// IDE-level totals (appears in both org and user reports)
    /// </summary>
    public class CopilotIdeTotal
    {
        [JsonPropertyName("ide")]
        public string? Ide { get; set; }

        [JsonPropertyName("user_initiated_interaction_count")]
        public int UserInitiatedInteractionCount { get; set; }

        [JsonPropertyName("code_generation_activity_count")]
        public int CodeGenerationActivityCount { get; set; }

        [JsonPropertyName("code_acceptance_activity_count")]
        public int CodeAcceptanceActivityCount { get; set; }

        [JsonPropertyName("loc_suggested_to_add_sum")]
        public int LocSuggestedToAddSum { get; set; }

        [JsonPropertyName("loc_added_sum")]
        public int LocAddedSum { get; set; }
    }

    // ============================================================
    // KEPT OLD CLASSES (still referenced by old commented-out code above)
    // These can be removed once migration is fully verified
    // ============================================================
    public class GitHubMetrics
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("total_active_users")]
        public int TotalActiveUsers { get; set; }

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("copilot_ide_code_completions")]
        public GitHubCodeCompletions? CodeCompletions { get; set; }

        [JsonPropertyName("copilot_ide_chat")]
        public GitHubIdeChat? IdeChat { get; set; }

        [JsonPropertyName("copilot_dotcom_chat")]
        public GitHubDotComChat? DotComChat { get; set; }

        [JsonPropertyName("copilot_dotcom_pull_requests")]
        public GitHubPullRequests? PullRequests { get; set; }
    }

    public class GitHubCodeCompletions
    {
        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("languages")]
        public List<GitHubLanguage>? Languages { get; set; }

        [JsonPropertyName("editors")]
        public List<GitHubEditor>? Editors { get; set; }
    }

    public class GitHubEditor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("models")]
        public List<GitHubModel>? Models { get; set; }
    }

    public class GitHubModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("is_custom_model")]
        public bool IsCustomModel { get; set; }

        [JsonPropertyName("custom_model_training_date")]
        public string? CustomModelTrainingDate { get; set; }

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("total_chats")]
        public int? TotalChats { get; set; }

        [JsonPropertyName("total_chat_copy_events")]
        public int? TotalChatCopyEvents { get; set; }

        [JsonPropertyName("total_chat_insertion_events")]
        public int? TotalChatInsertionEvents { get; set; }

        [JsonPropertyName("total_pr_summaries_created")]
        public int? TotalPRSummariesCreated { get; set; }

        [JsonPropertyName("languages")]
        public List<GitHubLanguage>? Languages { get; set; }
    }

    public class GitHubLanguage
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("total_code_suggestions")]
        public int? TotalCodeSuggestions { get; set; }

        [JsonPropertyName("total_code_acceptances")]
        public int? TotalCodeAcceptances { get; set; }

        [JsonPropertyName("total_code_lines_suggested")]
        public int? TotalLinesSuggested { get; set; }

        [JsonPropertyName("total_code_lines_accepted")]
        public int? TotalLinesAccepted { get; set; }
    }

    public class GitHubIdeChat
    {
        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("editors")]
        public List<GitHubEditor>? Editors { get; set; }
    }

    public class GitHubDotComChat
    {
        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("models")]
        public List<GitHubModel>? Models { get; set; }
    }

    public class GitHubPullRequests
    {
        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("repositories")]
        public List<GitHubRepository>? Repositories { get; set; }
    }

    public class GitHubRepository
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("models")]
        public List<GitHubModel>? Models { get; set; }
    }
}