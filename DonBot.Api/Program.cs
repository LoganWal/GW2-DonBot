using DonBot.Api.Endpoints;
using DonBot.Api.Registration;
using DonBot.Configuration;
using Microsoft.AspNetCore.Http.Features;
using Serilog;

RuntimeConfiguration.LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddRuntimeConfiguration(args, reloadOnChange: false);
builder.Services.AddPortableHostLifetimes();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

ApiServiceRegister.ConfigureServices(builder.Services, builder.Configuration);

var maxUploadBytes = builder.Configuration.GetValue<long>("Upload:MaxRequestBytes", 1_073_741_824);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);

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
app.MapGuildAdminEndpoints();
app.MapLiveRaidEndpoints();
app.MapSchedulingEndpoints();

app.Run();
