using LeaderboardApp.DTOs;
using LeaderboardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaderboardApp.Services
{
    public class LeaderboardService
    {
        private readonly GhcacDbContext _context;

        public LeaderboardService(GhcacDbContext context)
        {
            _context = context;
        }

        public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync()
        {
            return await _context.Leaderboardentries
            .Join(
                _context.Teams.Include(t => t.Participants), // Include Participants
                leaderboardEntry => leaderboardEntry.Teamid,
                team => team.Teamid,
                (leaderboardEntry, team) => new LeaderboardEntryDto
                {
                    TeamName = team.Name,
                    TeamId = team.Teamid,
                    TeamTagline = team.Tagline,
                    TeamIcon = team.Icon,
                    Score = leaderboardEntry.Score,
                    LastUpdated = leaderboardEntry.Lastupdated,
                    ParticipantNames = team.Participants
                                        .Select(p => $"{p.Nickname}")
                                        .ToList()
                }
            )
            .OrderByDescending(le => le.Score)
            .ToListAsync();
        }

        public async Task UpdateLeaderboardAsync()
        {
            var leaderboardEntries = await _context.Leaderboardentries.ToListAsync();

            foreach (var entry in leaderboardEntries)
            {
                // Calculate score from ParticipantScores (Score is already pre-calculated at insert time)
                var participantScores = await _context.Participantscores
                    .Where(ps => ps.Participant.Teamid == entry.Teamid)
                    .SumAsync(ps => ps.Score);

                entry.Score = (int)participantScores;
                entry.Lastupdated = DateTime.UtcNow;

                _context.Entry(entry).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
        }
    }
}
