using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using TaskFlow.Web.Services;

namespace TaskFlow.Web;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();
        builder.Services.AddDataProtection();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<TaskFlowUserDirectory>();
        var baseUrl = builder.Configuration["Api:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiAddress)
            || apiAddress.Scheme is not ("http" or "https")
            || (!builder.Environment.IsDevelopment() && apiAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Configure Api:BaseUrl with an absolute HTTPS URL outside Development.");
        }

        builder.Services.AddHttpClient<ITaskFlowApiClient, TaskFlowApiClient>((_, client) =>
        {
            client.BaseAddress = apiAddress;
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "TaskFlow.Web.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.SlidingExpiration = false;
            });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
