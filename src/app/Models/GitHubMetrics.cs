using System.Text.Json.Serialization;

namespace LeaderboardApp.Models
{
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
        public string Name { get; set; }

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("models")]
        public List<GitHubModel>? Models { get; set; }
    }

    public class GitHubModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

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
        public string Name { get; set; }

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
        public string Name { get; set; }

        [JsonPropertyName("total_engaged_users")]
        public int TotalEngagedUsers { get; set; }

        [JsonPropertyName("models")]
        public List<GitHubModel>? Models { get; set; }
    }
}