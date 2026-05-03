using DonBot.Api.Endpoints;
using DonBot.Api.Registration;
using Serilog;

var envFile = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (File.Exists(envFile))
    DotNetEnv.Env.Load(envFile);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

ApiServiceRegister.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("DonBotPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapLogsEndpoints();
app.MapUploadEndpoints();
app.MapStatsEndpoints();
app.MapLeaderboardEndpoints();
app.MapPointsEndpoints();
app.MapAccountEndpoints();

app.Run();
