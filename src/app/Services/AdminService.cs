using LeaderboardApp.Models;
using LeaderboardApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeaderboardApp.Services
{
    public class AdminService : IAdminService
    {
        private readonly GhcacDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly GitHubService _githubService;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            GhcacDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            GitHubService githubService,
            ILogger<AdminService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _githubService = githubService;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────
        //  Auth helpers
        // ────────────────────────────────────────────────────

        public bool IsAdminUser()
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

        public bool IsChallengeStarted()
        {
            return _configuration.GetValue<bool>("ChallengeSettings:ChallengeStarted", false);
        }

        // ────────────────────────────────────────────────────
        //  Read operations
        // ────────────────────────────────────────────────────

        public async Task<List<AdminParticipantViewModel>> GetAllParticipantsWithTeamsAsync()
        {
            var participants = await _context.Participants
                .Include(p => p.Team)
                .OrderBy(p => p.Firstname)
                .ThenBy(p => p.Lastname)
                .ToListAsync();

            return participants.Select(p => new AdminParticipantViewModel
            {
                ParticipantId = p.Participantid,
                FirstName = p.Firstname,
                LastName = p.Lastname,
                Nickname = p.Nickname,
                Email = p.Email,
                GitHubHandle = p.Githubhandle,
                TeamId = p.Teamid,
                TeamName = p.Team?.Name
            }).ToList();
        }

        public async Task<List<Team>> GetAllTeamsAsync()
        {
            return await _context.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        // ────────────────────────────────────────────────────
        //  Participant mutations (locked when challenge started)
        // ────────────────────────────────────────────────────

        public async Task MoveParticipantAsync(Guid participantId, Guid newTeamId)
        {
            var participant = await _context.Participants
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.Participantid == participantId)
                ?? throw new InvalidOperationException($"Participant {participantId} not found.");

            var newTeam = await _context.Teams.FindAsync(newTeamId)
                ?? throw new InvalidOperationException($"Team {newTeamId} not found.");

            var oldSlug = participant.Team?.GitHubSlug;
            var newSlug = newTeam.GitHubSlug;

            participant.Teamid = newTeamId;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(participant.Githubhandle))
            {
                await _githubService.MoveUserToTeamAsync(participant.Githubhandle, oldSlug, newSlug ?? string.Empty);
            }

            _logger.LogInformation(
                "Admin moved participant {ParticipantId} ({Email}) from team '{OldTeam}' to team '{NewTeam}'",
                participantId, participant.Email, oldSlug, newSlug);
        }

        public async Task UnassignParticipantAsync(Guid participantId)
        {
            var participant = await _context.Participants
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.Participantid == participantId)
                ?? throw new InvalidOperationException($"Participant {participantId} not found.");

            var oldSlug = participant.Team?.GitHubSlug;
            participant.Teamid = null;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(participant.Githubhandle) && !string.IsNullOrWhiteSpace(oldSlug))
            {
                await _githubService.RemoveUserFromTeamAsync(oldSlug, participant.Githubhandle);
            }

            _logger.LogInformation(
                "Admin unassigned participant {ParticipantId} ({Email}) from team '{OldTeam}'",
                participantId, participant.Email, oldSlug);
        }

        public async Task DeleteParticipantAsync(Guid participantId)
        {
            var participant = await _context.Participants
                .Include(p => p.Team)
                .Include(p => p.Participantscores)
                .FirstOrDefaultAsync(p => p.Participantid == participantId)
                ?? throw new InvalidOperationException($"Participant {participantId} not found.");

            var teamSlug = participant.Team?.GitHubSlug;

            // Remove from GitHub team first
            if (!string.IsNullOrWhiteSpace(participant.Githubhandle) && !string.IsNullOrWhiteSpace(teamSlug))
            {
                await _githubService.RemoveUserFromTeamAsync(teamSlug, participant.Githubhandle);
            }

            // Delete participant scores (FK constraint)
            _context.Participantscores.RemoveRange(participant.Participantscores);
            _context.Participants.Remove(participant);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin deleted participant {ParticipantId} ({Email})",
                participantId, participant.Email);
        }

        // ────────────────────────────────────────────────────
        //  Team mutations (locked when challenge started)
        // ────────────────────────────────────────────────────

        public async Task<Team> CreateTeamAdminAsync(Team team)
        {
            team.Teamid = Guid.NewGuid();

            // Default icon if none provided
            if (string.IsNullOrWhiteSpace(team.Icon))
                team.Icon = "https://img.icons8.com/fluency/48/team.png";

            // If no slug was provided, create team on GitHub and use the slug returned
            if (string.IsNullOrWhiteSpace(team.GitHubSlug))
            {
                var slug = await _githubService.CreateTeamAsync(team.Name);
                if (!string.IsNullOrWhiteSpace(slug))
                    team.GitHubSlug = slug;
                else
                    team.GitHubSlug = "notavailable"; // Default when GitHub is disabled
            }
            else
            {
                // Slug manually provided; the GitHub team should already exist or be created separately
                _logger.LogInformation(
                    "Creating team '{TeamName}' with manually supplied GitHub slug '{Slug}'",
                    team.Name, team.GitHubSlug);
            }

            // Original code (before fix):
            // if (string.IsNullOrWhiteSpace(team.GitHubSlug))
            // {
            //     var slug = await _githubService.CreateTeamAsync(team.Name);
            //     if (!string.IsNullOrWhiteSpace(slug))
            //         team.GitHubSlug = slug;
            // }

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin created team {TeamId} '{TeamName}' (slug: {Slug})",
                team.Teamid, team.Name, team.GitHubSlug);

            return team;
        }

        public async Task UpdateTeamAdminAsync(Team team)
        {
            var existing = await _context.Teams.FindAsync(team.Teamid)
                ?? throw new InvalidOperationException($"Team {team.Teamid} not found.");

            if (existing.GitHubSlug != team.GitHubSlug && !string.IsNullOrWhiteSpace(existing.GitHubSlug))
            {
                _logger.LogWarning(
                    "Admin changed GitHubSlug for team {TeamId} from '{OldSlug}' to '{NewSlug}'. " +
                    "The GitHub team is NOT renamed automatically - update the GitHub team slug manually if needed.",
                    team.Teamid, existing.GitHubSlug, team.GitHubSlug);
            }

            existing.Name = team.Name;
            // existing.Icon = team.Icon;
            existing.Icon = string.IsNullOrWhiteSpace(team.Icon) ? existing.Icon : team.Icon;
            existing.Tagline = team.Tagline;
            existing.GitHubSlug = team.GitHubSlug;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin updated team {TeamId} '{TeamName}' (slug: {Slug})",
                team.Teamid, team.Name, team.GitHubSlug);
        }

        public async Task DeleteTeamAdminAsync(Guid teamId)
        {
            var team = await _context.Teams
                .Include(t => t.Participants)
                .FirstOrDefaultAsync(t => t.Teamid == teamId)
                ?? throw new InvalidOperationException($"Team {teamId} not found.");

            var slug = team.GitHubSlug;

            // Remove GitHub team memberships and unassign participants from DB team
            foreach (var participant in team.Participants)
            {
                if (!string.IsNullOrWhiteSpace(participant.Githubhandle) && !string.IsNullOrWhiteSpace(slug))
                {
                    await _githubService.RemoveUserFromTeamAsync(slug, participant.Githubhandle);
                }
                participant.Teamid = null;
            }

            await _context.SaveChangesAsync();

            // Delete the GitHub team itself
            if (!string.IsNullOrWhiteSpace(slug))
            {
                await _githubService.DeleteTeamAsync(slug);
            }

            // Remove leaderboard entries for this team
            var leaderboardEntries = _context.Leaderboardentries.Where(e => e.Teamid == teamId);
            _context.Leaderboardentries.RemoveRange(leaderboardEntries);

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin deleted team {TeamId} '{TeamName}'", teamId, team.Name);
        }

        // ────────────────────────────────────────────────────
        //  GitHub sync (read-only; always available)
        // ────────────────────────────────────────────────────

        public async Task<List<TeamSyncReport>> SyncGitHubTeamsAsync()
        {
            var teams = await _context.Teams
                .Include(t => t.Participants)
                .Where(t => t.GitHubSlug != null && t.GitHubSlug != string.Empty)
                .ToListAsync();

            var reports = new List<TeamSyncReport>();

            foreach (var team in teams)
            {
                var report = new TeamSyncReport
                {
                    TeamName = team.Name,
                    GitHubSlug = team.GitHubSlug!
                };

                try
                {
                    var githubMembers = await _githubService.GetTeamMembersAsync(team.GitHubSlug);

                    if (githubMembers == null)
                    {
                        report.Error = "Failed to retrieve GitHub team members - check GitHub settings and PAT.";
                        reports.Add(report);
                        continue;
                    }

                    var githubLogins = githubMembers
                        .Select(m => m.Login.ToLowerInvariant())
                        .ToHashSet();

                    var dbHandles = team.Participants
                        .Where(p => !string.IsNullOrWhiteSpace(p.Githubhandle))
                        .Select(p => p.Githubhandle!.ToLowerInvariant())
                        .ToHashSet();

                    report.InGitHubNotInDb = githubLogins
                        .Except(dbHandles)
                        .OrderBy(x => x)
                        .ToList();

                    report.InDbNotInGitHub = dbHandles
                        .Except(githubLogins)
                        .OrderBy(x => x)
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing team '{TeamName}' (slug: {Slug})", team.Name, team.GitHubSlug);
                    report.Error = ex.Message;
                }

                reports.Add(report);
            }

            return reports;
        }
    }
}
