using LeaderboardApp.DTOs;
using LeaderboardApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaderboardApp.Controllers
{
    public class ScoringController : Controller
    {
        private readonly GhcacDbContext _context;

        public ScoringController(GhcacDbContext context)
        {
            _context = context;
        }

        // GET: /Scoring/ParticipantScores
        public async Task<IActionResult> ParticipantScores()
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

            return View(scores); // Return a view with scores, assumes you have a corresponding view file
        }

        [HttpPost]
        public async Task<IActionResult> AddScore(ParticipantScoreInputModel inputModel, string? returnUrl = null)
        {
            // Retrieve the activity to determine the weight and type
            var activity = await _context.Activities.FindAsync(inputModel.ActivityId);
            if (activity == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Activity not found." });
                }
                return NotFound("Activity not found.");
            }

            // Determine the time frame based on the frequency of the activity
            DateTime timeFrameStart = DateTime.UtcNow;
            switch (activity.Frequency.ToLower())
            {
                case "daily":
                    timeFrameStart = DateTime.UtcNow.Date;
                    break;
                case "weekly":
                    timeFrameStart = DateTime.UtcNow.AddDays(-((int)DateTime.UtcNow.DayOfWeek));
                    break;
                case "once":
                    var completedOnce = await _context.Participantscores
                                                      .AnyAsync(ps => ps.Participantid == inputModel.ParticipantId && ps.Activityid == inputModel.ActivityId);

                    if (completedOnce)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return Json(new { success = false, message = "This activity can only be completed once." });
                        }
                        return BadRequest("This activity can only be completed once.");
                    }
                    break;
            }

            // Check if the participant has already completed this activity in the given time frame
            var existingScore = await _context.Participantscores
                                              .Where(ps => ps.Participantid == inputModel.ParticipantId
                                                        && ps.Activityid == inputModel.ActivityId
                                                        && ps.Timestamp >= timeFrameStart)
                                              .FirstOrDefaultAsync();

            if (existingScore != null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Activity has already been completed for the specified time frame." });
                }
                return BadRequest("Activity has already been completed for the specified time frame.");
            }

            // Calculate the score
            decimal score = activity.Weighttype == "Fixed" ? activity.Weight : activity.Weight * (inputModel.CustomScore ?? 1);

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

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Score added successfully." });
            }

            // If a return URL is provided, redirect back to it
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("ParticipantScores");
        }


        // Method to update leaderboard based on participant scores
        private async Task UpdateLeaderboard(Guid participantId)
        {
            var participant = await _context.Participants.Include(p => p.Team).FirstOrDefaultAsync(p => p.Participantid == participantId);
            if (participant == null) return;

            var teamId = participant.Teamid;

            var teamScores = await _context.Participantscores
                                           .Where(ps => ps.Participant.Teamid == teamId)
                                           .SumAsync(ps => (int)ps.Score);

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