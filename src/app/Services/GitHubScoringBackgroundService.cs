using LeaderboardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaderboardApp.Services
{
    public class GitHubScoringBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GitHubScoringBackgroundService> _logger;
        private readonly bool _enabled;
        private readonly TimeSpan _interval = TimeSpan.FromHours(6);

        public GitHubScoringBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<GitHubScoringBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool>("GitHubSettings:Enabled", false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("GitHubScoringBackgroundService is disabled (GitHubSettings:Enabled=false)");
                return;
            }

            // Initial delay to let the app start up
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            // Fix any teams with invalid "notavailable" slug
            await FixInvalidTeamSlugsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllTeamsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GitHubScoringBackgroundService");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ProcessAllTeamsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GhcacDbContext>();
            var scoringService = scope.ServiceProvider.GetRequiredService<ScoringService>();
            var leaderboardService = scope.ServiceProvider.GetRequiredService<LeaderboardService>();

            var teams = await context.Teams
                .Where(t => t.GitHubSlug != null && t.GitHubSlug != "")
                .ToListAsync(stoppingToken);

            _logger.LogInformation("Processing GitHub scores for {Count} teams", teams.Count);

            foreach (var team in teams)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var result = await scoringService.InsertTeamGitHubScoresAsync(team.GitHubSlug!);
                    if (result)
                    {
                        _logger.LogInformation("Successfully processed GitHub scores for team {TeamSlug}", team.GitHubSlug);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process GitHub scores for team {TeamSlug}", team.GitHubSlug);
                }
            }

            // Update the full leaderboard after processing all teams
            try
            {
                await leaderboardService.UpdateLeaderboardAsync();
                _logger.LogInformation("Leaderboard updated after GitHub scoring run");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update leaderboard after GitHub scoring");
            }
        }

        private async Task FixInvalidTeamSlugsAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<GhcacDbContext>();
                var githubService = scope.ServiceProvider.GetRequiredService<GitHubService>();

                var invalidTeams = await context.Teams
                    .Where(t => t.GitHubSlug == "notavailable")
                    .ToListAsync(stoppingToken);

                if (invalidTeams.Count == 0) return;

                _logger.LogInformation("Found {Count} teams with invalid 'notavailable' slug. Fixing...", invalidTeams.Count);

                foreach (var team in invalidTeams)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var slug = await githubService.CreateTeamAsync(team.Name);
                    if (!string.IsNullOrWhiteSpace(slug))
                    {
                        team.GitHubSlug = slug;
                        _logger.LogInformation("Fixed team '{TeamName}' slug to '{Slug}'", team.Name, slug);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing invalid team slugs");
            }
        }
    }
}
