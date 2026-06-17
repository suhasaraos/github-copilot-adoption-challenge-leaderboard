using LeaderboardApp.Models;
using LeaderboardApp.Security;
using LeaderboardApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeaderboardApp.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly GhcacDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;
        private readonly GitHubService _gitHubService;
        private readonly ILogger<UserController> _logger;

        public UserController(GhcacDbContext context, IConfiguration configuration, TokenService tokenService, GitHubService gitHubService, ILogger<UserController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var maxParticipants = _configuration.GetValue<int>("ChallengeSettings:MaxParticipantsPerTeam");

                var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue("preferred_username")
                    ?? User.FindFirstValue("emails");

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Unable to get email details for the signed-in user");
                    return RedirectToAction("Error", "Home", new { message = "Unable to retrieve your email address. Please contact support." });
                }

                var user = await _context.Participants.FirstOrDefaultAsync(p => p.Email == email);

                if (user == null)
                {
                    try
                    {
                        var newUserId = Guid.NewGuid();
                        await CreateParticipant(email, newUserId);
                        user = await _context.Participants.FindAsync(newUserId);

                        if (user == null)
                        {
                            _logger.LogError("Failed to create new participant with ID {ParticipantId}", newUserId);
                            return RedirectToAction("Error", "Home", new { message = "Failed to create your profile. Please try again later." });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating new participant for email {Email}", email);
                        return RedirectToAction("Error", "Home", new { message = "Failed to create your profile. Please try again later." });
                    }
                }
                else
                {
                    try
                    {
                        // Update LastLogin timestamp for existing users
                        user.Lastlogin = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating lastlogin for participant {ParticipantId}", user.Participantid);
                        // Continue execution - non-critical error
                    }
                }

                // Fetch teams with participants less than the configured max number
                List<Team> teams;
                HashSet<Guid> fullTeamIds;

                try
                {
                    teams = await _context.Teams
                        .Include(t => t.Participants)
                        .ToListAsync();

                    fullTeamIds = teams
                        .Where(t => t.Participants.Count >= maxParticipants)
                        .Select(t => t.Teamid)
                        .ToHashSet();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching teams data");
                    teams = new List<Team>();
                    fullTeamIds = new HashSet<Guid>();
                }

                ViewBag.FullTeamIds = fullTeamIds;
                ViewBag.Teams = teams;
                var startDateStr = _configuration.GetValue<string>("ChallengeSettings:ChallengeStartDate");
                var challengeStarted = DateTime.TryParse(startDateStr, out var startDate) && DateTime.UtcNow >= startDate;
                ViewBag.ChallengeStarted = challengeStarted;

                // Get last activity time from GitHub Service
                try
                {
                    if (!string.IsNullOrEmpty(user.Githubhandle))
                    {
                        var lastAct = await _gitHubService.GetLastUserActivity(user.Githubhandle);
                        ViewBag.LastActivity = lastAct ?? "No recent activity found.";
                    }
                    else
                    {
                        ViewBag.LastActivity = "No GitHub handle provided.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching GitHub activity for user {GitHubHandle}", user.Githubhandle);
                    ViewBag.LastActivity = "Unable to fetch activity data.";
                }

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in Profile action");
                return RedirectToAction("Error", "Home", new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] Participant model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest(new { message = "No data provided." });
                }

                var startDateStr = _configuration.GetValue<string>("ChallengeSettings:ChallengeStartDate");
                var challengeStarted = DateTime.TryParse(startDateStr, out var startDate) && DateTime.UtcNow >= startDate;
                if (challengeStarted)
                {
                    return BadRequest(new { message = "The challenge has started. You cannot edit your profile." });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid data provided.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
                }

                var participant = await _context.Participants.SingleOrDefaultAsync(p => p.Email == model.Email);
                if (participant == null)
                {
                    return NotFound(new { message = "Participant not found." });
                }

                // Detect GitHub handle change
                var oldGitHubHandle = participant.Githubhandle;
                var newGitHubHandle = model.Githubhandle;

                // Query to check GitHubHandle and MsLearnHandle conflicts
                if (!string.IsNullOrWhiteSpace(newGitHubHandle) || !string.IsNullOrWhiteSpace(model.Mslearnhandle))
                {
                    var conflicts = await _context.Participants
                        .Where(p => p.Participantid != participant.Participantid)
                        .Where(p =>
                            (!string.IsNullOrWhiteSpace(newGitHubHandle) && p.Githubhandle == newGitHubHandle) ||
                            (!string.IsNullOrWhiteSpace(model.Mslearnhandle) && p.Mslearnhandle == model.Mslearnhandle)
                        )
                        .ToListAsync();

                    if (conflicts.Any())
                    {
                        var conflictMessages = new List<string>();

                        if (conflicts.Any(p => p.Githubhandle == newGitHubHandle))
                        {
                            conflictMessages.Add("This GitHub handle is already taken by another participant.");
                        }

                        if (conflicts.Any(p => p.Mslearnhandle == model.Mslearnhandle))
                        {
                            conflictMessages.Add("This Microsoft Learn handle is already taken by another participant.");
                        }

                        return BadRequest(new { message = string.Join(" ", conflictMessages) });
                    }
                }

                // Detect team change
                var oldTeamId = participant.Teamid;
                var newTeamId = model.Teamid;

                // Check if user is trying to join a team that's already at capacity
                if (newTeamId != oldTeamId && newTeamId.HasValue)
                {
                    var maxParticipants = _configuration.GetValue<int>("ChallengeSettings:MaxParticipantsPerTeam");
                    var currentTeamSize = await _context.Participants
                        .CountAsync(p => p.Teamid == newTeamId.Value);

                    if (currentTeamSize >= maxParticipants)
                    {
                        return BadRequest(new { message = "This team has reached its maximum capacity." });
                    }
                }

                // Update participant data
                participant.Firstname = model.Firstname;
                participant.Lastname = model.Lastname;
                participant.Nickname = model.Nickname;
                participant.Githubhandle = newGitHubHandle;
                participant.Mslearnhandle = model.Mslearnhandle;

                if (!challengeStarted)
                {
                    participant.Teamid = newTeamId;
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while updating participant {ParticipantId}", participant.Participantid);
                    return StatusCode(500, new { message = "Database error occurred while updating your profile." });
                }

                // Handle GitHub operations
                bool gitHubUpdateSuccess = true;
                string gitHubMessage = "";

                // Handle GitHub handle change
                if (newTeamId.HasValue && !string.IsNullOrWhiteSpace(newGitHubHandle) &&
                   (!string.IsNullOrWhiteSpace(oldGitHubHandle) && oldGitHubHandle != newGitHubHandle))
                {
                    try
                    {
                        var newTeam = await _context.Teams.FindAsync(newTeamId.Value);

                        if (newTeam != null && !string.IsNullOrWhiteSpace(newTeam.GitHubSlug))
                        {
                            gitHubUpdateSuccess = await _gitHubService.MoveUserToTeamAsync(newGitHubHandle!, null, newTeam.GitHubSlug);

                            if (!gitHubUpdateSuccess)
                            {
                                gitHubMessage = "Profile updated, but failed to update GitHub handle in GitHub.";
                                _logger.LogWarning("Failed to update GitHub handle for user {ParticipantId}, new handle: {GitHubHandle}",
                                    participant.Participantid, newGitHubHandle);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        gitHubUpdateSuccess = false;
                        gitHubMessage = "Profile updated, but GitHub team operations failed.";
                        _logger.LogError(ex, "Error updating GitHub handle for user {ParticipantId}", participant.Participantid);
                    }
                }

                // Move GitHub user to new team if team changed and GitHubHandle exists
                if (gitHubUpdateSuccess && oldTeamId != newTeamId && newTeamId.HasValue &&
                    !string.IsNullOrWhiteSpace(participant.Githubhandle))
                {
                    try
                    {
                        // First ensure we can get the new team - this is critical
                        var newTeam = await _context.Teams.FindAsync(newTeamId.Value);
                        if (newTeam == null || string.IsNullOrWhiteSpace(newTeam.GitHubSlug))
                        {
                            gitHubUpdateSuccess = false;
                            gitHubMessage = "Profile updated, but new team not found or missing GitHub details.";
                            _logger.LogWarning("New team {TeamId} not found or has no GitHub slug", newTeamId);
                        }
                        else
                        {
                            // Get old team information separately - don't use parallel execution
                            Team? oldTeam = null;
                            if (oldTeamId.HasValue)
                            {
                                try
                                {
                                    oldTeam = await _context.Teams.FindAsync(oldTeamId.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error retrieving old team {TeamId}, continuing with team change", oldTeamId);
                                    // Continue even if we can't get the old team - we can still add to new team
                                }
                            }

                            // Extract the old team's slug
                            var oldSlug = oldTeam?.GitHubSlug;

                            // Use an increased timeout for GitHub API call
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            try
                            {
                                // Log detailed information before the GitHub API call
                                _logger.LogInformation(
                                    "Attempting to move GitHub user {GitHubHandle} from team {OldTeam} to {NewTeam}",
                                    participant.Githubhandle,
                                    oldSlug ?? "none",
                                    newTeam.GitHubSlug);

                                gitHubUpdateSuccess = await _gitHubService.MoveUserToTeamAsync(
                                    participant.Githubhandle!,
                                    oldSlug,
                                    newTeam.GitHubSlug);

                                if (!gitHubUpdateSuccess)
                                {
                                    gitHubMessage = "Profile updated, but failed to move user to GitHub team.";
                                    _logger.LogWarning("Failed to move user {ParticipantId} from team {OldTeamId} to {NewTeamId}",
                                        participant.Participantid, oldTeamId, newTeamId);
                                }
                                else
                                {
                                    _logger.LogInformation("Successfully moved user {ParticipantId} to GitHub team {TeamSlug}",
                                        participant.Participantid, newTeam.GitHubSlug);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                gitHubUpdateSuccess = false;
                                gitHubMessage = "Profile updated, but GitHub team operation timed out.";
                                _logger.LogError("GitHub API call timed out while moving user {GitHubHandle} to team {TeamSlug}",
                                    participant.Githubhandle, newTeam.GitHubSlug);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        gitHubUpdateSuccess = false;
                        gitHubMessage = "Profile updated, but GitHub team operations failed.";
                        _logger.LogError(ex, "Error moving user {ParticipantId} to new team {TeamId}",
                            participant.Participantid, newTeamId);
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = gitHubUpdateSuccess ? "Profile updated successfully!" : gitHubMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in UpdateProfile action");
                return StatusCode(500, new { message = "An unexpected error occurred while updating your profile." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClickLink([FromBody] ClickLinkInputModel model)
        {
            try
            {
                if (model == null || model.ActivityId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid activity data." });
                }

                // Get the logged-in user's ParticipantId
                var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue("preferred_username")
                    ?? User.FindFirstValue("emails");

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("User email not found during ClickLink action");
                    return Unauthorized(new { success = false, message = "User not properly authenticated." });
                }

                var participant = await _context.Participants.FirstOrDefaultAsync(p => p.Email == email);
                if (participant == null)
                {
                    _logger.LogWarning("Participant not found for email {Email} during ClickLink action", email);
                    return Unauthorized(new { success = false, message = "User profile not found." });
                }

                var participantId = participant.Participantid;

                // Check if the user has already completed the activity
                var alreadyClicked = await _context.Participantscores
                    .AnyAsync(ps => ps.Participantid == participantId
                                  && ps.Activityid == model.ActivityId);

                if (!alreadyClicked)
                {
                    // If not clicked before, add the score for this activity
                    var activity = await _context.Activities.FindAsync(model.ActivityId);
                    if (activity == null)
                    {
                        return NotFound(new { success = false, message = "Activity not found." });
                    }

                    if (!activity.Name.ToLowerInvariant().Contains("link"))
                    {
                        return BadRequest(new { success = false, message = "Activity not of type link." });
                    }

                    // Get first challenge with this activity ID or use a default ID
                    int challengeId = 1; // Default value
                    var challenge = await _context.Challenges.FirstOrDefaultAsync(c => c.ActivityId == model.ActivityId);
                    if (challenge != null)
                    {
                        challengeId = challenge.ChallengeId;
                    }

                    var participantScore = new Participantscore
                    {
                        Participantid = participantId,
                        Activityid = model.ActivityId,
                        Challengeid = challengeId,
                        Score = activity.Weight,
                        Timestamp = DateTime.UtcNow,
                        Teamid = participant.Teamid ?? Guid.Empty // Use team ID or default if not in a team
                    };

                    _context.Participantscores.Add(participantScore);

                    try
                    {
                        await _context.SaveChangesAsync();
                        return Ok(new { success = true, message = "Score added!" });
                    }
                    catch (DbUpdateException ex)
                    {
                        _logger.LogError(ex, "Database error while adding participant score for {ParticipantId}, Activity {ActivityId}",
                            participantId, model.ActivityId);
                        return StatusCode(500, new { success = false, message = "Failed to record your activity." });
                    }
                }

                // If already clicked, return success but no score added
                return Ok(new { success = true, message = "You've already clicked this link. No score added." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in ClickLink action");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred." });
            }
        }

        private async Task<Participant?> CreateParticipant(string email, Guid? sid = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Attempted to create participant with null or empty email");
                throw new ArgumentException("Email cannot be null or empty", nameof(email));
            }

            if (sid == null)
            {
                sid = Guid.NewGuid();
            }

            try
            {
                // Check if participant already exists
                var existingParticipant = await _context.Participants.SingleOrDefaultAsync(p => p.Email == email);
                if (existingParticipant != null)
                {
                    return existingParticipant;
                }

                // Extract username part for firstname
                string firstName = "User";
                try
                {
                    if (email.Contains('@'))
                    {
                        firstName = email.Split('@')[0];
                    }
                }
                catch
                {
                    // If there's any error splitting the email, use default
                    firstName = "User";
                }

                // Create a new participant
                var participant = new Participant
                {
                    Participantid = sid.Value,
                    Email = email,
                    Firstname = firstName,
                    Lastname = "Participant",
                    Nickname = NicknameService.GetNickName(),
                    Externalid = Guid.NewGuid(),
                    Passcode = null,
                    Passcodeexpiration = null,
                    Lastlogin = DateTime.UtcNow
                };

                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new participant with ID {ParticipantId} for email {Email}",
                    participant.Participantid, email);

                return participant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating participant for email {Email}", email);
                throw;
            }
        }
    }

    public class ClickLinkInputModel
    {
        public int ActivityId { get; set; }
    }
}
