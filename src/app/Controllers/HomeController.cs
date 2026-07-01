using LeaderboardApp.Services;
using LeaderboardApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaderboardApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly LeaderboardService _leaderboardService;
        private readonly ChallengesService _challengesService;
        private readonly IConfiguration _configuration;

        public HomeController(LeaderboardService leaderboardService, ChallengesService challengesService, IConfiguration configuration  )
        {
            _leaderboardService = leaderboardService;
            _challengesService = challengesService;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var leaderboard = await _leaderboardService.GetLeaderboardAsync();
            var challenges = await _challengesService.GetChallengesAsync();

            var model = new HomeViewModel
            {
                Leaderboard = leaderboard,
                Challenges = challenges
            };

            return View(model);
        }

        public IActionResult Terms()
        {            
            var supportEmail = _configuration["SmtpSettings:SenderEmail"] ?? string.Empty;
            
            // Pass the email to the view using ViewData
            ViewData["SupportEmail"] = supportEmail;

            return View();
        }

        public async Task<IActionResult> FullLeaderBoard()
        {
            var leaderboard = await _leaderboardService.GetLeaderboardAsync();           
            return View(leaderboard);
        }
        
        [Authorize]
        public async Task<IActionResult> AllChallenges()
        {
            var challenges = await _challengesService.GetChallengesAsync();
            return View(challenges);
        }        
    }
}
