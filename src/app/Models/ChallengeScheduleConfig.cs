namespace LeaderboardApp.Models
{
    /// <summary>
    /// Top-level configuration object bound from the "ChallengeSchedule" section
    /// in challenge-schedule.json.
    /// </summary>
    public class ChallengeScheduleConfig
    {
        /// <summary>
        /// How many hours after a challenge's ReleaseDate it should display the "NEW" badge.
        /// Defaults to 24 hours.
        /// </summary>
        public int NewBadgeDurationHours { get; set; } = 24;


        /// <summary>
        /// Ordered list of challenges with their release dates.
        /// Challenges not listed here fall back to the PostedDate field in the database.
        /// </summary>
        public List<ChallengeScheduleEntry> Challenges { get; set; } = new();
    }
}
