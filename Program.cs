using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using LIS.Data;
using LIS.Models;
using LIS.Services;
using LIS.Components;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageReports", policy => policy.RequireRole("SuperAdmin", "LabAdmin", "Admin"));
    options.AddPolicy("ViewReports", policy => policy.RequireRole("SuperAdmin", "LabAdmin", "Admin", "Doctor", "HeadNurse", "LabManager"));
    options.AddPolicy("SystemSettingsOnly", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("ManageSettings", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("ViewAuditLogs", policy => policy.RequireRole("SuperAdmin"));
});

// Cookie config
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});



// App Services
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DropdownService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<PatientService>();
builder.Services.AddScoped<ReportUploadService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<RoleAccessService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<StaffRegistrationService>();

// Email Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Blazor + Razor Pages (for login/logout)
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// QuestPDF
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
