namespace LeaderboardApp.DTOs
{
    public class ChallengeDto
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime PostedDate { get; set; }
        public int ActivityId { get; set; }
        public int ChallengeId { get; set; }
        public bool IsCompleted { get; set; }

        /// <summary>
        /// True when the challenge was released within the configured NewBadgeDurationHours window.
        /// Computed at query time from the challenge-schedule.json release date.
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// 1-based sort order from challenge-schedule.json.
        /// Scheduled challenges sort before unscheduled ones (int.MaxValue).
        /// </summary>
        public int DisplayOrder { get; set; } = int.MaxValue;
    }
}
