using LeaderboardApp.DTOs;
using LeaderboardApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaderboardApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ParticipantScoresController : ControllerBase
    {
        private readonly GhcacDbContext _context;

        public ParticipantScoresController(GhcacDbContext context)
        {
            _context = context;
        }

        // GET: api/participantscores
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ParticipantScoreDetailDto>>> GetParticipantScores()
        {
            var scores = await _context.Participants
                                       .Include(p => p.Team)
                                       .Select(p => new ParticipantScoreDetailDto
                                       {
                                           ParticipantId = p.Participantid,
                                           ParticipantName = p.Firstname + " " + p.Lastname,
                                           TeamName = p.Team.Name,
                                           TotalScore = _context.Participantscores
                                                                .Where(ps => ps.Participantid == p.Participantid)
                                                                .Sum(ps => ps.Score)
                                       })
                                       .OrderBy(p => p.ParticipantName)
                                       .ToListAsync();

            return Ok(scores);
        }

        [HttpPost]
        public async Task<ActionResult<Participantscore>> CreateParticipantScore([FromBody] ParticipantScoreInputModel inputModel)
        {
            // Retrieve the activity to determine the weight and type
            var activity = await _context.Activities.FindAsync(inputModel.ActivityId);
            if (activity == null)
            {
                return NotFound("Activity not found.");
            }

            // Calculate the score
            decimal score = 0;
            if (activity.Weighttype == "Fixed")
            {
                score = activity.Weight;
            }
            else if (activity.Weighttype == "Multiplier")
            {
                score = activity.Weight * (inputModel.CustomScore ?? 1);
            }

            // Create the ParticipantScore entry
            var participantScore = new Participantscore
            {
                Participantid = inputModel.ParticipantId,
                Activityid = inputModel.ActivityId,
                Score = score,
                Timestamp = DateTime.UtcNow
            };

            _context.Participantscores.Add(participantScore);
            await _context.SaveChangesAsync();

            // Update the leaderboard
            await UpdateLeaderboard(participantScore.Participantid);

            return CreatedAtAction(nameof(GetParticipantScores), new { id = participantScore.Scoreid }, participantScore);
        }

        private bool ParticipantScoreExists(int id)
        {
            return _context.Participantscores.Any(e => e.Scoreid == id);
        }

        // Method to update leaderboard based on participant scores
        private async Task UpdateLeaderboard(Guid participantId)
        {
            var participant = await _context.Participants.Include(p => p.Team).FirstOrDefaultAsync(p => p.Participantid == participantId);
            if (participant == null) return;

            var teamId = participant.Teamid;

            var teamScores = await _context.Participantscores
                                           .Where(ps => ps.Participant.Teamid == teamId)
                                           .SumAsync(ps => (int)ps.Score); // Casting decimal to int

            var leaderboardEntry = await _context.Leaderboardentries.FirstOrDefaultAsync(le => le.Teamid == teamId);

            if (leaderboardEntry != null)
            {
                leaderboardEntry.Score = teamScores;
                leaderboardEntry.Lastupdated = DateTime.UtcNow;
            }
            else
            {
                _context.Leaderboardentries.Add(new Leaderboardentry
                {
                    Leaderboardentryid = Guid.NewGuid(),
                    Teamid = teamId,
                    Teamname = participant.Team!.Name,
                    Score = teamScores,
                    Lastupdated = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
