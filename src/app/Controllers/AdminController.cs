using LeaderboardApp.Models;
using LeaderboardApp.Services;
using LeaderboardApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaderboardApp.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;
        private readonly ScoringService _scoringService;
        private readonly LeaderboardService _leaderboardService;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger, ScoringService scoringService, LeaderboardService leaderboardService)
        {
            _adminService = adminService;
            _logger = logger;
            _scoringService = scoringService;
            _leaderboardService = leaderboardService;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Guard helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Redirects to Home if the caller is not an admin. Returns null when access is allowed.</summary>
        private IActionResult? RequireAdmin()
        {
            if (!_adminService.IsAdminUser())
                return RedirectToAction("Index", "Home");
            return null;
        }

        /// <summary>Returns an error redirect when reorganization is locked (challenge has started).</summary>
        private IActionResult? RequireNotStarted(string returnAction)
        {
            if (_adminService.IsChallengeStarted())
            {
                TempData["Error"] = "Reorganization is locked because the challenge has already started.";
                return RedirectToAction(returnAction);
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dashboard
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;

            ViewBag.IsLocked = _adminService.IsChallengeStarted();
            ViewBag.ParticipantCount = (await _adminService.GetAllParticipantsWithTeamsAsync()).Count;
            ViewBag.TeamCount = (await _adminService.GetAllTeamsAsync()).Count;
            return View();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Participants
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Participants()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;

            var vm = new AdminParticipantsViewModel
            {
                Participants = await _adminService.GetAllParticipantsWithTeamsAsync(),
                AllTeams = await _adminService.GetAllTeamsAsync(),
                IsLocked = _adminService.IsChallengeStarted()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveParticipant(Guid participantId, Guid newTeamId)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Participants") is { } locked) return locked;

            try
            {
                await _adminService.MoveParticipantAsync(participantId, newTeamId);
                TempData["Success"] = "Participant moved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving participant {ParticipantId}", participantId);
                TempData["Error"] = $"Failed to move participant: {ex.Message}";
            }

            return RedirectToAction("Participants");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnassignParticipant(Guid participantId)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Participants") is { } locked) return locked;

            try
            {
                await _adminService.UnassignParticipantAsync(participantId);
                TempData["Success"] = "Participant unassigned from team.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning participant {ParticipantId}", participantId);
                TempData["Error"] = $"Failed to unassign participant: {ex.Message}";
            }

            return RedirectToAction("Participants");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteParticipant(Guid participantId)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Participants") is { } locked) return locked;

            try
            {
                await _adminService.DeleteParticipantAsync(participantId);
                TempData["Success"] = "Participant deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting participant {ParticipantId}", participantId);
                TempData["Error"] = $"Failed to delete participant: {ex.Message}";
            }

            return RedirectToAction("Participants");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Teams
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Teams()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;

            var vm = new AdminTeamsViewModel
            {
                Teams = await _adminService.GetAllTeamsAsync(),
                IsLocked = _adminService.IsChallengeStarted()
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult CreateTeam()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (_adminService.IsChallengeStarted())
            {
                TempData["Error"] = "Reorganization is locked because the challenge has already started.";
                return RedirectToAction("Teams");
            }
            return View(new Team());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(Team team)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Teams") is { } locked) return locked;

            // DEBUG: Log raw form values
            _logger.LogWarning("RAW FORM DATA: Name='{Name}', Tagline='{Tagline}', Icon='{Icon}', GitHubSlug='{Slug}'",
                Request.Form["Name"].ToString(), Request.Form["Tagline"].ToString(),
                Request.Form["Icon"].ToString(), Request.Form["GitHubSlug"].ToString());
            _logger.LogWarning("BOUND MODEL: Name='{Name}', Tagline='{Tagline}'", team.Name, team.Tagline);

            // Convert empty Icon to null so [Url] validation passes
            if (string.IsNullOrWhiteSpace(team.Icon))
            {
                team.Icon = null;
                ModelState.Remove("Icon");
            }

            // Convert empty GitHubSlug to null
            if (string.IsNullOrWhiteSpace(team.GitHubSlug))
            {
                team.GitHubSlug = null;
                ModelState.Remove("GitHubSlug");
            }

            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        _logger.LogWarning("ModelState error for '{Key}': {Error}", kvp.Key, error.ErrorMessage);
                    }
                }
                return View(team);
            }

            try
            {
                await _adminService.CreateTeamAdminAsync(team);
                TempData["Success"] = $"Team '{team.Name}' created successfully.";
                return RedirectToAction("Teams");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team");
                ModelState.AddModelError(string.Empty, $"Failed to create team: {ex.Message}");
                return View(team);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditTeam(Guid teamId)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (_adminService.IsChallengeStarted())
            {
                TempData["Error"] = "Reorganization is locked because the challenge has already started.";
                return RedirectToAction("Teams");
            }

            var teams = await _adminService.GetAllTeamsAsync();
            var team = teams.FirstOrDefault(t => t.Teamid == teamId);
            if (team == null)
            {
                TempData["Error"] = "Team not found.";
                return RedirectToAction("Teams");
            }

            return View(team);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeam(Guid teamId, Team team)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Teams") is { } locked) return locked;

            team.Teamid = teamId;

            if (!ModelState.IsValid)
                return View(team);

            try
            {
                await _adminService.UpdateTeamAdminAsync(team);
                TempData["Success"] = $"Team '{team.Name}' updated successfully.";
                return RedirectToAction("Teams");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team {TeamId}", teamId);
                ModelState.AddModelError(string.Empty, $"Failed to update team: {ex.Message}");
                return View(team);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeam(Guid teamId)
        {
            if (RequireAdmin() is { } forbidden) return forbidden;
            if (RequireNotStarted("Teams") is { } locked) return locked;

            try
            {
                await _adminService.DeleteTeamAdminAsync(teamId);
                TempData["Success"] = "Team deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team {TeamId}", teamId);
                TempData["Error"] = $"Failed to delete team: {ex.Message}";
            }

            return RedirectToAction("Teams");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Scoring Refresh
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshScores()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;

            try
            {
                var teams = await _adminService.GetAllTeamsAsync();
                int processed = 0;

                foreach (var team in teams.Where(t => !string.IsNullOrEmpty(t.GitHubSlug)))
                {
                    var result = await _scoringService.InsertTeamGitHubScoresAsync(team.GitHubSlug!);
                    if (result) processed++;
                }

                await _leaderboardService.UpdateLeaderboardAsync();
                TempData["Success"] = $"GitHub scores refreshed for {processed} team(s). Leaderboard updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing scores");
                TempData["Error"] = $"Failed to refresh scores: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sync (always available - read-only comparison)
        // ─────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Sync()
        {
            if (RequireAdmin() is { } forbidden) return forbidden;

            var reports = await _adminService.SyncGitHubTeamsAsync();
            var vm = new SyncResultViewModel
            {
                Reports = reports,
                GitHubDisabled = !HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetValue<bool>("GitHubSettings:Enabled", false)
            };

            return View(vm);
        }
    }
}
