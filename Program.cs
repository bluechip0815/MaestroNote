using MaestroNotes.Data;
using MaestroNotes.Data.Ai;
using MaestroNotes.Services;
using MaestroNotes.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

SetLogging(builder);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure AI Settings
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("AiSettings"));

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// string? serverVersion = builder.Configuration.GetConnectionString("ServerVersion"); // Unused
builder.Services.AddDbContext<MusicContext>(option => option.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 38))));

// Register Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Register Music Service
builder.Services.AddScoped<MusicService>();

// Register Authentication
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Register AI Service and Provider
builder.Services.AddScoped<AiService>();
builder.Services.AddHttpClient("AiClient"); // Register named client if needed, or just let Factory handle it.

builder.Services.AddScoped<IAiProvider>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value;
    var httpClient = HttpClientFactory.Create(settings);

    return settings.Provider.ToLower() switch
    {
        "gemini" => new GeminiProvider(httpClient, settings.ApiKey, settings.ProviderUrl),
        "anthropic" => new AnthropicProvider(httpClient, settings.ApiKey, settings.ProviderUrl),
        "chatgpt" or _ => new OpenAiProvider(httpClient, settings.ApiKey, settings.ProviderUrl),
    };
});

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

// Perform data migration / Ensure DB creation
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MusicContext>();
        context.Database.EnsureCreated();

        // Seed initial admin user if no users exist
        if (!context.Users.Any())
        {
            context.Users.Add(new User
            {
                Name = "Admin",
                Email = "admin@example.com",
                UserLevel = UserLevel.Admin
            });
            context.SaveChanges();
            Log.Information("Seeded default Admin user.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred during database initialization.");
    }
}

app.Run();

static void SetLogging(WebApplicationBuilder builder)
{
    // Optional: Serilog internal diagnostics to help when sinks fail
    // SelfLog.Enable(msg => Console.Error.WriteLine(msg));

    // 1) Read desired path from config
    var configuredPath = builder.Configuration["Serilog:WriteTo:1:Args:path"] ?? "logs\\log.txt";

    // 2) Make it absolute based on content root (more predictable under IIS)
    var fullPath = Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredPath);

    // 3) Ensure directory exists; if not possible, fall back to console-only file path or skip file sink
    try
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }
    catch
    {
        // If directory creation fails, fall back to a safe location or skip file sink
        fullPath = Path.Combine(builder.Environment.ContentRootPath, "log.txt");
    }

    // 4) Build logger primarily from configuration
    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext();

        // Optionally override the file path from config with the resolved absolute path:
        // (Easiest way is to keep config relative and just pass fullPath in code)
        lc.WriteTo.File(fullPath, rollingInterval: RollingInterval.Day);
    });
}
