using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeaderboardApp.Models;
using LeaderboardApp.Services;
using System.Text;

namespace LeaderboardApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamsController : ControllerBase
    {
        private readonly GhcacDbContext _context;
        private readonly GitHubService _githubService;
        private readonly ILogger<TeamsController> _logger;

        public TeamsController(GhcacDbContext context, GitHubService githubService, ILogger<TeamsController> logger)
        {
            _context = context;
            _githubService = githubService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams()
        {
            return Ok(await _context.Teams.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeam(Guid id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null)
            {
                return NotFound();
            }
            return Ok(team);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam([FromBody] Team team)
        {
            team.Teamid = Guid.NewGuid();
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTeam), new { id = team.Teamid }, team);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] Team team)
        {
            if (id != team.Teamid)
            {
                return BadRequest();
            }

            _context.Entry(team).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Teams.Any(e => e.Teamid == id))
                {
                    return NotFound();
                }
                throw;
            }
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null)
            {
                return NotFound();
            }

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("sync-github-participants")]
        public async Task<IActionResult> SyncGitHubParticipants()
        {
            _logger.LogInformation("Starting GitHub team participants synchronization (DB as source of truth)");

            // Create log entries as a list of structured objects instead of text
            var logEntries = new List<object>
            {
                new { type = "header", message = "GitHub Teams Synchronization Log" },
                new { type = "info", message = $"Started at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC" },
                new { type = "info", message = "Mode: Database is source of truth" }
            };

            // Also keep a text log for traditional logging
            var syncLog = new StringBuilder();
            syncLog.AppendLine("GitHub Teams Synchronization Log");
            syncLog.AppendLine("===============================");
            syncLog.AppendLine($"Started at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            syncLog.AppendLine("Mode: Database is source of truth");
            syncLog.AppendLine();

            // Step 1: Get all teams from DB with their GitHubSlug
            var teamsWithSlug = await _context.Teams
                .Where(t => !string.IsNullOrWhiteSpace(t.GitHubSlug))
                .Include(t => t.Participants.Where(p => !string.IsNullOrWhiteSpace(p.Githubhandle)))
                .ToListAsync();

            logEntries.Add(new { type = "info", message = $"Found {teamsWithSlug.Count} teams with GitHub slugs in database" });
            syncLog.AppendLine($"Found {teamsWithSlug.Count} teams with GitHub slugs in database");

            if (!teamsWithSlug.Any())
            {
                logEntries.Add(new { type = "warning", message = "No teams with GitHub slugs found. Nothing to sync." });
                syncLog.AppendLine("No teams with GitHub slugs found. Nothing to sync.");
                _logger.LogInformation("No teams with GitHub slugs found. Nothing to sync.");

                return Ok(new
                {
                    success = true,
                    message = "No teams with GitHub slugs found. Nothing to sync.",
                    log = logEntries,
                    textLog = syncLog.ToString(),
                    stats = new { teamsProcessed = 0, participantsAdded = 0, participantsRemoved = 0 }
                });
            }

            var teamResults = new List<object>();
            var summary = new Dictionary<string, Dictionary<string, int>>();
            var totalAdded = 0;
            var totalRemoved = 0;

            // Step 2: For each team, sync participants with GitHub
            foreach (var team in teamsWithSlug)
            {
                var teamLog = new List<object>
                {
                    new { type = "team-header", message = $"Processing team: {team.Name} (GitHub slug: {team.GitHubSlug})" }
                };

                syncLog.AppendLine();
                syncLog.AppendLine($"Processing team: {team.Name} (GitHub slug: {team.GitHubSlug})");

                // Get GitHub team members
                var githubMembers = await _githubService.GetTeamMembersAsync(team.GitHubSlug);

                if (githubMembers == null)
                {
                    teamLog.Add(new { type = "error", message = $"Failed to retrieve GitHub members for team {team.Name}" });
                    syncLog.AppendLine($"  Failed to retrieve GitHub members for team {team.Name}");
                    _logger.LogWarning("Failed to retrieve GitHub members for team {TeamName}", team.Name);

                    teamResults.Add(new
                    {
                        team = new { id = team.Teamid, name = team.Name, slug = team.GitHubSlug },
                        status = "failed",
                        error = "Failed to retrieve GitHub members",
                        log = teamLog
                    });

                    continue;
                }

                // Map of the current state
                var dbParticipants = team.Participants
                    .Where(p => !string.IsNullOrWhiteSpace(p.Githubhandle))
                    .ToDictionary(p => p.Githubhandle.ToLower(), p => p);
                var githubHandles = githubMembers.Select(m => m.Login.ToLower()).ToHashSet();

                // In DB, but not in GitHub - need to add to GitHub
                var participantsToAddToGitHub = dbParticipants.Keys
                    .Where(dbh => !githubHandles.Contains(dbh))
                    .ToList();

                // In GitHub, but not in DB - need to remove from GitHub
                var participantsToRemoveFromGitHub = githubHandles
                    .Where(gh => !dbParticipants.ContainsKey(gh))
                    .ToList();

                // Log the state
                teamLog.Add(new { type = "info", message = $"DB Participants: {dbParticipants.Count}" });
                teamLog.Add(new { type = "info", message = $"GitHub Members: {githubHandles.Count}" });
                teamLog.Add(new { type = "info", message = $"To Add to GitHub: {participantsToAddToGitHub.Count}" });
                teamLog.Add(new { type = "info", message = $"To Remove from GitHub: {participantsToRemoveFromGitHub.Count}" });

                syncLog.AppendLine($"  DB Participants: {dbParticipants.Count}");
                syncLog.AppendLine($"  GitHub Members: {githubHandles.Count}");
                syncLog.AppendLine($"  To Add to GitHub: {participantsToAddToGitHub.Count}");
                syncLog.AppendLine($"  To Remove from GitHub: {participantsToRemoveFromGitHub.Count}");

                // Track changes in summary
                summary[team.GitHubSlug] = new Dictionary<string, int>
                {
                    ["db"] = dbParticipants.Count,
                    ["github"] = githubHandles.Count,
                    ["added"] = 0,
                    ["removed"] = 0
                };

                var addResults = new List<object>();
                var removeResults = new List<object>();

                // Process participants to add to GitHub
                foreach (var dbParticipantHandle in participantsToAddToGitHub)
                {
                    var participant = dbParticipants[dbParticipantHandle];

                    // Add to GitHub team
                    var success = await _githubService.MoveUserToTeamAsync(
                        participant.Githubhandle,
                        null, // No old team to remove from
                        team.GitHubSlug);

                    if (success)
                    {
                        var logMessage = $"Added participant {participant.Nickname} ({dbParticipantHandle}) to GitHub team {team.GitHubSlug}";
                        teamLog.Add(new { type = "success", message = logMessage });
                        syncLog.AppendLine($"  {logMessage}");
                        _logger.LogInformation("Added participant {Nickname} ({GitHubHandle}) to GitHub team {Team}",
                            participant.Nickname, dbParticipantHandle, team.GitHubSlug);

                        summary[team.GitHubSlug]["added"]++;
                        totalAdded++;

                        addResults.Add(new
                        {
                            status = "success",
                            participant = new
                            {
                                id = participant.Participantid,
                                name = $"{participant.Firstname} {participant.Lastname}",
                                nickname = participant.Nickname,
                                githubHandle = participant.Githubhandle
                            }
                        });
                    }
                    else
                    {
                        var logMessage = $"FAILED to add participant {participant.Nickname} ({dbParticipantHandle}) to GitHub team {team.GitHubSlug}";
                        teamLog.Add(new { type = "error", message = logMessage });
                        syncLog.AppendLine($"  {logMessage}");
                        _logger.LogError("Failed to add participant {Nickname} ({GitHubHandle}) to GitHub team {Team}",
                            participant.Nickname, dbParticipantHandle, team.GitHubSlug);

                        addResults.Add(new
                        {
                            status = "error",
                            participant = new
                            {
                                id = participant.Participantid,
                                name = $"{participant.Firstname} {participant.Lastname}",
                                nickname = participant.Nickname,
                                githubHandle = participant.Githubhandle
                            },
                            error = "GitHub API call failed"
                        });
                    }
                }

                // Process participants to remove from GitHub
                foreach (var githubHandle in participantsToRemoveFromGitHub)
                {
                    // Remove from GitHub team
                    var success = await _githubService.RemoveUserFromTeamAsync(team.GitHubSlug, githubHandle);

                    if (success)
                    {
                        var logMessage = $"Removed participant {githubHandle} from GitHub team {team.GitHubSlug}";
                        teamLog.Add(new { type = "success", message = logMessage });
                        syncLog.AppendLine($"  {logMessage}");
                        _logger.LogInformation("Removed participant {GitHubHandle} from GitHub team {Team}",
                            githubHandle, team.GitHubSlug);

                        summary[team.GitHubSlug]["removed"]++;
                        totalRemoved++;

                        removeResults.Add(new
                        {
                            status = "success",
                            githubHandle
                        });
                    }
                    else
                    {
                        var logMessage = $"FAILED to remove participant {githubHandle} from GitHub team {team.GitHubSlug}";
                        teamLog.Add(new { type = "error", message = logMessage });
                        syncLog.AppendLine($"  {logMessage}");
                        _logger.LogError("Failed to remove participant {GitHubHandle} from GitHub team {Team}",
                            githubHandle, team.GitHubSlug);

                        removeResults.Add(new
                        {
                            status = "error",
                            githubHandle,
                            error = "GitHub API call failed"
                        });
                    }
                }

                teamResults.Add(new
                {
                    team = new { id = team.Teamid, name = team.Name, slug = team.GitHubSlug },
                    status = "processed",
                    stats = new
                    {
                        dbParticipants = dbParticipants.Count,
                        githubMembers = githubHandles.Count,
                        added = summary[team.GitHubSlug]["added"],
                        removed = summary[team.GitHubSlug]["removed"]
                    },
                    addOperations = addResults,
                    removeOperations = removeResults,
                    log = teamLog
                });
            }

            // Add summary to log
            var summaryLog = new List<object>
            {
                new { type = "summary-header", message = "Synchronization Summary" },
                new { type = "info", message = $"Total participants added to GitHub: {totalAdded}" },
                new { type = "info", message = $"Total participants removed from GitHub: {totalRemoved}" }
            };

            syncLog.AppendLine();
            syncLog.AppendLine("Synchronization Summary");
            syncLog.AppendLine("======================");
            syncLog.AppendLine($"Total participants added to GitHub: {totalAdded}");
            syncLog.AppendLine($"Total participants removed from GitHub: {totalRemoved}");
            syncLog.AppendLine();

            var teamSummaries = new List<object>();
            foreach (var team in summary)
            {
                var teamSummaryLog = new List<string> {
            $"Team {team.Key}:",
            $"  DB participants: {team.Value["db"]}",
            $"  GitHub members before sync: {team.Value["github"]}",
            $"  Added to GitHub: {team.Value["added"]}",
            $"  Removed from GitHub: {team.Value["removed"]}",
            $"  GitHub members after sync: {team.Value["db"]}"
        };

                teamSummaries.Add(new
                {
                    teamSlug = team.Key,
                    dbCount = team.Value["db"],
                    githubCountBefore = team.Value["github"],
                    added = team.Value["added"],
                    removed = team.Value["removed"],
                    githubCountAfter = team.Value["db"]
                });

                foreach (var line in teamSummaryLog)
                {
                    syncLog.AppendLine(line);
                }
            }

            var completionTime = DateTime.UtcNow;
            logEntries.Add(new { type = "info", message = $"Completed at: {completionTime:yyyy-MM-dd HH:mm:ss} UTC" });
            syncLog.AppendLine();
            syncLog.AppendLine($"Completed at: {completionTime:yyyy-MM-dd HH:mm:ss} UTC");

            _logger.LogInformation("GitHub team participants synchronization completed. Added to GitHub: {Added}, Removed from GitHub: {Removed}",
                totalAdded, totalRemoved);

            // Return a structured JSON response with detailed logs and statistics
            return Ok(new
            {
                success = true,
                message = $"GitHub team participants synchronized. {totalAdded} added to GitHub, {totalRemoved} removed from GitHub.",
                stats = new
                {
                    teamsProcessed = teamsWithSlug.Count,
                    teamsSucceeded = teamResults.Count(t => ((dynamic)t).status != "failed"),
                    teamsFailed = teamResults.Count(t => ((dynamic)t).status == "failed"),
                    participantsAdded = totalAdded,
                    participantsRemoved = totalRemoved,                    
                    endTime = completionTime.ToString("yyyy-MM-dd HH:mm:ss UTC")
                },
                teams = teamResults,
                teamSummaries,
                log = logEntries,
                textLog = syncLog.ToString() // Include text log for backwards compatibility
            });
        }
    }
}
