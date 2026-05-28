using MaestroNotes.Data;
using MaestroNotes.Data.Ai;
using MaestroNotes.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

SetLogging(builder);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MaestroNotesAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var principal = context.Principal;
                if (principal != null)
                {
                    var tokenClaim = principal.FindFirst("CookieMagic");
                    if (tokenClaim != null && Guid.TryParse(tokenClaim.Value, out Guid tokenGuid))
                    {
                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<MusicContext>();
                        var token = await dbContext.LoginTokens.FirstOrDefaultAsync(t => t.Token == tokenGuid);

                        if (token == null || token.CreatedAt < DateTime.UtcNow.AddDays(-30))
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    }
                    else
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                }
            }
        };
    });

// Register AI Service and Provider
builder.Services.AddScoped<AiService>();

builder.Services.AddHttpClient("AiClient", client =>
{
    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value;
    var handler = new HttpClientHandler();
    if (!string.IsNullOrEmpty(settings.Proxy))
    {
        handler.Proxy = new System.Net.WebProxy(settings.Proxy, settings.BypassLocal);
        handler.UseProxy = true;
        handler.UseDefaultCredentials = true;
    }
    return handler;
});

builder.Services.AddScoped<IAiProvider>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value;
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = factory.CreateClient("AiClient");
    var logger = sp.GetRequiredService<ILogger<OpenAiProvider>>();

    return settings.Provider.ToLower() switch
    {
        "gemini" => new GeminiProvider(httpClient, settings.ApiKey, settings.ProviderUrl),
        "anthropic" => new AnthropicProvider(httpClient, settings.ApiKey, settings.ProviderUrl),
        "chatgpt" or _ => new OpenAiProvider(httpClient, settings.ApiKey, settings.ProviderUrl, logger, settings.ListModels),
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
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

    // Configure Serilog to use Console only, respecting user request to not store logs in repo
    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console();
    });
}
