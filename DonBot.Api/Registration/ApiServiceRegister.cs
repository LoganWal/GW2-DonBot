using DonBot.Api.Services;
using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Services.SecretsServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DonBot.Api.Registration;

public static class ApiServiceRegister
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISecretService, SecretServices>();

        services.AddDbContextFactory<DatabaseContext>((serviceProvider, options) =>
        {
            var secretService = serviceProvider.GetRequiredService<ISecretService>();
            var connectionString = secretService.FetchDonBotSqlConnectionString();
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("DonBot"));
        });

        services.AddHttpClient();
        services.AddHttpClient("gw2-api", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(8);
        });

        services.AddScoped(typeof(IDatabaseUpdateService<>), typeof(DatabaseUpdateService<>));
        services.AddScoped<IEntityService, EntityService>();
        services.AddScoped<IRaidLifecycleService, RaidLifecycleService>();
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IDataModelGenerationService, DataModelGenerationService>();

        // Message generation chain (used by web-side StartRaid for the raid alert ping).
        services.AddTransient<IFooterService, FooterService>();
        services.AddTransient<IPvEFightSummaryService, PvEFightSummaryService>();
        services.AddTransient<IWvWFightSummaryService, WvWFightSummaryService>();
        services.AddTransient<IRaidReportService, RaidReportService>();
        services.AddTransient<IMessageGenerationService, MessageGenerationService>();
        services.AddScoped<IRotationAnalysisService, RotationAnalysisService>();
        services.AddScoped<IRaidNotifier, RaidNotifier>();

        services.AddSingleton<ILogUploadProgressService, LogUploadProgressService>();
        services.AddSingleton<LogUploadPipelineService>();
        services.AddSingleton<TusFileMapping>();
        services.AddSingleton<DiscordRestClientProvider>();
        services.AddSingleton<IUserGuildsService, UserGuildsService>();
        services.AddMemoryCache();
        services.AddHostedService(sp => sp.GetRequiredService<LogUploadPipelineService>());

        var jwtKey = configuration["DonBotJwtKey"] ?? Environment.GetEnvironmentVariable("DonBotJwtKey")
            ?? throw new InvalidOperationException("'DonBotJwtKey' is not configured. Set it in appsettings.user.json or the .env file.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["donbot_token"];
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        services.AddCors(options =>
        {
            options.AddPolicy("DonBotPolicy", policy =>
            {
                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins);
                }
                else
                {
                    // No explicit origins configured: allow any localhost origin for local dev.
                    policy.SetIsOriginAllowed(origin =>
                        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                        uri.Host == "localhost");
                }

                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
                      .WithExposedHeaders(
                          "X-Log-Upload-Id",
                          "Upload-Offset",
                          "Upload-Length",
                          "Tus-Version",
                          "Tus-Resumable",
                          "Tus-Max-Size",
                          "Location"
                      );
            });
        });
    }
}
