namespace LeaderboardApp.Models
{
    /// <summary>
    /// Represents a single entry in the challenge release schedule configuration.
    /// </summary>
    public class ChallengeScheduleEntry
    {
        /// <summary>
        /// The database ChallengeId this entry applies to.
        /// </summary>
        public int ChallengeId { get; set; }

        /// <summary>
        /// The 1-based display order for this challenge in all challenge lists.
        /// Lower numbers appear first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// The UTC date/time from which this challenge becomes visible to all participants.
        /// Accepts ISO 8601 format (e.g. "2026-03-10" or "2026-03-10T09:00:00Z").
        /// </summary>
        public DateTime ReleaseDate { get; set; }
    }
}
