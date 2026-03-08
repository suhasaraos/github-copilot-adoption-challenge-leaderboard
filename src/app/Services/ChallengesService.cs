using LeaderboardApp.DTOs;
using LeaderboardApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LeaderboardApp.Services
{
    public class ChallengesService
    {
        private readonly GhcacDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ChallengesService> _logger;
        private readonly IOptionsSnapshot<ChallengeScheduleConfig> _scheduleConfig;
        private readonly IConfiguration _configuration;

        private Guid? _cachedUserId; 

        public ChallengesService(
            GhcacDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ChallengesService> logger,
            IOptionsSnapshot<ChallengeScheduleConfig> scheduleConfig,
            IConfiguration configuration)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scheduleConfig = scheduleConfig ?? throw new ArgumentNullException(nameof(scheduleConfig));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<List<ChallengeDto>?> GetChallengesAsync()
        {
            var userId = await GetUserIdAsync();

            if (!userId.HasValue)
            {
                return null;
            }

            var completedChallengeIds = await _context.Participantscores
                .Where(ps => ps.Participantid == userId.Value)
                .Select(ps => ps.Challengeid)
                .ToListAsync();

            var completedIdsSet = completedChallengeIds.ToHashSet();

            // Fetch all challenges with valid ActivityIds. No PostedDate filter at the DB level;
            // availability is resolved in-memory against the schedule config below.
            var allChallenges = await _context.Challenges
                .Where(c => c.ActivityId != null &&
                            (c.ActivityId == 10 || c.ActivityId == 11 || c.ActivityId == 12 || c.ActivityId == 13))
                .Select(c => new ChallengeDto
                {
                    Title = c.Title,
                    Content = c.Content,
                    PostedDate = c.PostedDate ?? DateTime.MinValue,
                    ActivityId = c.ActivityId ?? 0,
                    ChallengeId = c.ChallengeId,
                    IsCompleted = completedIdsSet.Contains(c.ChallengeId)
                })
                .ToListAsync();

            // Build a quick lookup from the schedule config (challenge-schedule.json)
            var schedule = _scheduleConfig.Value;
            var scheduleLookup = schedule.Challenges
                .ToDictionary(e => e.ChallengeId);

            var now = DateTime.UtcNow;
            var newBadgeCutoff = now.AddHours(-schedule.NewBadgeDurationHours);

            // Admins bypass the release-date filter and see all challenges immediately.
            var isAdmin = IsAdminUser(schedule);

            // Filter, annotate, and sort challenges according to the schedule.
            var result = allChallenges
                .Where(c =>
                {
                    if (isAdmin) return true; // admins see everything

                    if (scheduleLookup.TryGetValue(c.ChallengeId, out var entry))
                    {
                        // Scheduled: visible only on or after the configured ReleaseDate
                        return entry.ReleaseDate <= now;
                    }
                    else
                    {
                        // Unscheduled: fall back to the DB PostedDate field
                        return c.PostedDate == DateTime.MinValue || c.PostedDate <= now;
                    }
                })
                .Select(c =>
                {
                    if (scheduleLookup.TryGetValue(c.ChallengeId, out var entry))
                    {
                        // Override PostedDate with the schedule's ReleaseDate so the
                        // displayed date matches the config rather than the DB column.
                        c.PostedDate = entry.ReleaseDate;
                        // Mark as NEW for the configured number of hours after release
                        c.IsNew = entry.ReleaseDate >= newBadgeCutoff;
                        c.DisplayOrder = entry.Order;
                    }
                    else
                    {
                        // No schedule entry — DB PostedDate is already set; keep it.
                        c.IsNew = false;
                        c.DisplayOrder = int.MaxValue;
                    }
                    return c;
                })
                // Scheduled challenges appear first in configured order;
                // unscheduled challenges follow, sorted newest PostedDate first.
                .OrderBy(c => c.DisplayOrder)
                .ThenByDescending(c => c.PostedDate)
                .ToList();

            return result;
        }


        public async Task<ChallengeDto?> GetChallengeByIdAsync(int challengeId)
        {
            var userId = await GetUserIdAsync();

            var challengeQuery = _context.Challenges
                .Where(c => c.ChallengeId == challengeId)
                .Select(c => new ChallengeDto
                {
                    Title = c.Title,
                    Content = c.Content,
                    PostedDate = c.PostedDate ?? DateTime.MinValue,
                    ActivityId = c.ActivityId ?? 0,
                    ChallengeId = c.ChallengeId,
                    IsCompleted = userId.HasValue
                        ? _context.Participantscores
                            .Any(ps => ps.Participantid == userId.Value && ps.Challengeid == c.ChallengeId)
                        : false
                });

            var challenge = await challengeQuery.FirstOrDefaultAsync();
            return challenge;
        }

        public async Task<bool> MarkChallengeCompleted(int challengeId, string? validationData = null)
        {
            var userId = await GetUserIdAsync();
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User must be logged in to complete a challenge.");
            }

            var challenge = await _context.Challenges
                .Include(c => c.Activity)
                .FirstOrDefaultAsync(c => c.ChallengeId == challengeId);

            if (challenge == null || !challenge.ActivityId.HasValue)
            {
                throw new InvalidOperationException("Challenge or associated activity not found.");
            }

            if (await IsChallengeAlreadyCompleted(userId.Value, challengeId))
            {
                return false; // Challenge already completed :)
            }

            var teamId = await GetUserTeamIdAsync(userId.Value);
            if (!teamId.HasValue)
            {
                throw new InvalidOperationException("User is not associated with a team.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var participantScore = CreateParticipantScore(userId.Value, challenge, teamId.Value, validationData);
                _context.Participantscores.Add(participantScore);
                await _context.SaveChangesAsync();

                // Now check if this completion means all team members have completed the challenge
                // and award bonus points if needed
                await AwardTeamBonusForChallenge(teamId.Value, challengeId);

                await UpdateLeaderboardAsync(teamId.Value);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to mark challenge {ChallengeId} as completed for user {UserId}", challengeId, userId);
                throw new InvalidOperationException("Failed to mark challenge as completed.", ex);
            }

            return true;
        }



        public async Task<bool> IsChallengeAlreadyCompleted(Guid userId, int challengeId)
        {
            return await _context.Participantscores
                .AnyAsync(ps => ps.Participantid == userId && ps.Challengeid == challengeId);
        }

        public async Task<Guid?> GetUserTeamIdAsync(Guid userId)
        {
            return await _context.Participants
                .Where(p => p.Participantid == userId)
                .Select(p => p.Teamid)
                .FirstOrDefaultAsync();
        }

        private Participantscore CreateParticipantScore(Guid userId, Challenge challenge, Guid teamId, string? validationLink)
        {
            return new Participantscore
            {
                Participantid = userId,
                Activityid = challenge.ActivityId.Value,
                Challengeid = challenge.ChallengeId,
                Score = challenge.Activity?.Weight ?? 0,
                Timestamp = DateTime.UtcNow,
                Teamid = teamId,
                Validationlink = validationLink
            };
        }

        public async Task UpdateLeaderboardAsync(Guid teamId)
        {           
            var teamScores = await _context.Participantscores
                .Where(ps => ps.Teamid == teamId)
                .Join(_context.Activities,
                    ps => ps.Activityid,
                    a => a.Activityid,
                    (ps, a) => new
                    {
                        ps.Score
                    })
                .SumAsync(x => x.Score);

            var leaderboardEntry = await _context.Leaderboardentries.FirstOrDefaultAsync(le => le.Teamid == teamId); if (leaderboardEntry != null)
            {
                leaderboardEntry.Score = (int)teamScores;
                leaderboardEntry.Lastupdated = DateTime.UtcNow;
            }
            else
            {
                var team = _context.Teams.Where(t => t.Teamid == teamId).FirstOrDefault();
                _context.Leaderboardentries.Add(new Leaderboardentry
                {
                    Leaderboardentryid = Guid.NewGuid(),
                    Teamid = teamId,
                    Teamname = team?.Name ?? "Unknown",
                    Score = (int)teamScores,
                    Lastupdated = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<Guid?> GetUserIdAsync()
        {
            if (_cachedUserId.HasValue)
            {
                return _cachedUserId;
            }

            var email = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("preferred_username")
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("emails");

            if (!string.IsNullOrEmpty(email))
            {
                var participant = await _context.Participants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Email == email);

                _cachedUserId = participant?.Participantid;
            }

            return _cachedUserId;
        }

        /// <summary>
        /// Returns true if the currently authenticated user's email is in the Admin
        /// list in ChallengeSettings (appsettings.json). Comparison is case-insensitive.
        /// </summary>
        private bool IsAdminUser(ChallengeScheduleConfig _)
        {
            var adminEmails = _configuration.GetSection("ChallengeSettings:Admin").Get<List<string>>();
            if (adminEmails == null || adminEmails.Count == 0)
                return false;

            var email = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("preferred_username")
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("emails");

            if (string.IsNullOrEmpty(email))
                return false;

            return adminEmails.Any(a => a.Equals(email, StringComparison.OrdinalIgnoreCase));
        }


        // To check if all team members have completed a challenge
        private async Task<bool> HaveAllTeamMembersCompletedChallenge(Guid teamId, int challengeId)
        {
            // Get all participants in the team
            var teamParticipants = await _context.Participants
                .Where(p => p.Teamid == teamId)
                .ToListAsync();

            if (!teamParticipants.Any())
            {
                return false;
            }

            // Check if each participant has completed this challenge
            foreach (var participant in teamParticipants)
            {
                var hasCompleted = await _context.Participantscores
                    .AnyAsync(ps => ps.Participantid == participant.Participantid && ps.Challengeid == challengeId);

                if (!hasCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        // Check if team has already received a bonus for this challenge
        private async Task<bool> HasTeamAlreadyReceivedBonus(Guid teamId, int challengeId)
        {
            const int teamBonusActivityId = 14; // Team bonus activity ID

            return await _context.Participantscores
                .AnyAsync(ps => ps.Teamid == teamId && 
                          ps.Challengeid == challengeId && 
                          ps.Activityid == teamBonusActivityId &&
                          ps.Validationlink == "TeamBonus");
        }

        // To award team bonus points
        private async Task AwardTeamBonusForChallenge(Guid teamId, int challengeId)
        {
            // Check if the team has already received a bonus for this challenge
            if (await HasTeamAlreadyReceivedBonus(teamId, challengeId))
            {
                _logger.LogInformation("Team {TeamId} has already received a bonus for challenge {ChallengeId}", teamId, challengeId);
                return;
            }

            // Check if all team members have completed the challenge
            if (await HaveAllTeamMembersCompletedChallenge(teamId, challengeId))
            {
                // Award bonus points by creating a special participant score entry
                // We'll use the first team member's ID for this special entry
                var firstTeamMember = await _context.Participants
                    .Where(p => p.Teamid == teamId)
                    .FirstOrDefaultAsync();

                if (firstTeamMember != null)
                {
                    const int teamBonusActivityId = 14; // Team bonus activity ID
                    
                    // Get the bonus score from the database instead of hardcoding it
                    var teamBonusActivity = await _context.Activities
                        .FirstOrDefaultAsync(a => a.Activityid == teamBonusActivityId);
                    
                    if (teamBonusActivity == null)
                    {
                        _logger.LogError("TeamBonus activity (ID: {ActivityId}) not found in the database", teamBonusActivityId);
                        return;
                    }

                    // Add a special score entry for the team bonus
                    var bonusScore = new Participantscore
                    {
                        Participantid = firstTeamMember.Participantid,
                        Activityid = teamBonusActivityId,
                        Challengeid = challengeId,
                        Score = teamBonusActivity.Weight, // Use the weight from the database
                        Timestamp = DateTime.UtcNow,
                        Teamid = teamId,
                        Validationlink = "TeamBonus"
                    };

                    _context.Participantscores.Add(bonusScore);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Added {BonusPoints} bonus points to team {TeamId} for all members completing challenge {ChallengeId}", 
                        teamBonusActivity.Weight, teamId, challengeId);
                }
            }
        }

    }
}
