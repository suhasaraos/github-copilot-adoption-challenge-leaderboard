using LeaderboardApp.Models;
using LeaderboardApp.Security;
using LeaderboardApp.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;


namespace LeaderboardApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddEnvironmentVariables();


            // Configure Kestrel for flexible HTTPS
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                if (builder.Environment.IsProduction())
                {
                    // If running in Azure App Service, check native app service env variable. Use the default HTTPS port 443 and assume the certificate is managed by Azure.
                    var isAzureAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
                    if (!isAzureAppService)
                    {
                        try
                        {
                            Console.WriteLine("Attempting to load certificate");
                            var certPath = Environment.GetEnvironmentVariable("CertPath");
                            var certPassword = Environment.GetEnvironmentVariable("CertPassword");
                            var cert = new X509Certificate2(certPath, certPassword);

                            serverOptions.ListenAnyIP(8081, listenOptions =>
                            {
                                listenOptions.UseHttps(cert);
                            });
                        }
                        catch (Exception ex)
                        {
                            // Log the exception or handle it as appropriate for your application
                            Console.WriteLine($"Failed to load certificate: {ex.Message}");
                            throw; // Re-throw the exception if you want to prevent the application from starting with an invalid certificate
                        }
                    }
                }
            });

            // Register JwtSettings from configuration
            var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
            builder.Services.Configure<JwtSettings>(jwtSettingsSection);
            var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

            // Register challenge schedule configuration
            builder.Services.Configure<ChallengeScheduleConfig>(
                builder.Configuration.GetSection("ChallengeSchedule"));

            // Configure JWT Authentication - Comment below section out for TAL SSO
            //builder.Services.AddAuthentication(options =>
            //{
            //    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            //})
            //.AddJwtBearer(options =>
            //{
            //    options.TokenValidationParameters = new TokenValidationParameters
            //    {
            //        ValidateIssuer = true,
            //        ValidateAudience = true,
            //        ValidateLifetime = true,
            //        ValidateIssuerSigningKey = true,
            //        ValidIssuer = jwtSettings.Issuer,
            //        ValidAudience = jwtSettings.Audience,
            //        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            //        ClockSkew = TimeSpan.Zero // Optional: remove the default 5 minutes of clock skew
            //    };

            //    options.Events = new JwtBearerEvents
            //    {
            //        OnAuthenticationFailed = context =>
            //        {
            //            // Log or handle the exception for authentication failure if necessary
            //            return Task.CompletedTask;
            //        },
            //        OnTokenValidated = context =>
            //        {
            //            // Handle token validation and claims if necessary
            //            return Task.CompletedTask;
            //        }
            //    };
            //});

            // Add authorization service
            //builder.Services.AddAuthorization();
            //End of commented out section for TAL SSO

            //AAD SSO for TAL
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })            
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

            //Register application-specific services
            builder.Services.AddScoped<TokenService>();
            builder.Services.AddScoped<PasscodeEmailService>();

            builder.Services.AddScoped<LeaderboardService>();
            builder.Services.AddScoped<ChallengesService>();            
            builder.Services.AddHttpClient<GitHubService>();
            builder.Services.AddScoped<LearnService>();
            builder.Services.AddScoped<ScoringService>();

            // Add services to the container, including support for MVC and Web API
            builder.Services.AddControllersWithViews()  // Support for MVC
                .AddJsonOptions(options =>
                {
                    // To avoid circular reference errors in JSON serialization
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                });

            //// Add PostgreSQL DB Context
            //builder.Services.AddDbContext<GhcacDbContext>(options =>
            //    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            //// Add DbContext for executing raw SQL. No entity mappings within this context
            //builder.Services.AddDbContext<InitialDbContext>(options =>
            //    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Read DB provider from config
            var dbProvider = builder.Configuration["Database:Provider"]?.ToLower();
            var postgresConnection = builder.Configuration.GetConnectionString("PostgreSQL");
            var sqlConnection = builder.Configuration.GetConnectionString("SqlServer");

            if (dbProvider == "postgresql")
            {
                builder.Services.AddDbContext<GhcacDbContext>(options =>
                    options.UseNpgsql(postgresConnection));

                builder.Services.AddDbContext<InitialDbContext>(options =>
                    options.UseNpgsql(postgresConnection));
            }
            else if (dbProvider == "sqlserver")
            {
                builder.Services.AddDbContext<GhcacDbContext>(options =>
                    options.UseSqlServer(sqlConnection));

                builder.Services.AddDbContext<InitialDbContext>(options =>
                    options.UseSqlServer(sqlConnection));
            }
            else
            {
                throw new Exception("Unsupported DB provider specified in configuration.");
            }


            // Add DatabaseInitializer as a scoped service
            builder.Services.AddScoped<DatabaseInitializer>();

            // Configure Swagger/OpenAPI for API documentation
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();
            app.UseStaticFiles();

            // Initialize the database on the first run
            try
            {
                using var scope = app.Services.CreateScope();
                var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                dbInitializer.InitializeDatabaseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Init failed: {ex.Message}");
            }

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Register custom middleware before Authentication and Authorization - For Email Auth
            //app.UseMiddleware<JwtTokenCookieMiddleware>();

            // Enable authentication and authorization middleware
            app.UseAuthentication();
            app.UseAuthorization();

            // Map controllers for both API and MVC
            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Run the application
            app.Run();
        }
    }
}
