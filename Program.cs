using MaestroNotes.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

SetLogging(builder);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
string? serverVersion = builder.Configuration.GetConnectionString("ServerVersion");
builder.Services.AddDbContext<MusicContext>(option => option.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 38))));
builder.Services.AddScoped<MusicService>();
builder.Services.AddScoped<AiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

Log.Information("Starting web host");

// Perform data migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MusicContext>();
        DataMigrationService.MigrateData(context);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred during data migration.");
    }
}

app.Run();
static void SetLogging(WebApplicationBuilder builder)
{
    string? logFilePath = builder.Configuration["Serilog:WriteTo:1:Args:path"];
    if (logFilePath == null)
    {
        logFilePath = ".\\log.txt";
    }
    else
    {
        try
        {
            int idx = logFilePath.LastIndexOf("\\");
            string logDirectory = (idx == -1) ? logFilePath : logFilePath.Substring(0, idx);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }
        catch (Exception)
        {
            logFilePath = Directory.GetCurrentDirectory() + "\\log.txt";
        }
    }

    Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Debug()
       .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)  // Redirect Microsoft logs
       .Enrich.FromLogContext()
       .WriteTo.Console()  // Log to console
       .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)  // Log to absolute path
       .CreateLogger();

}
