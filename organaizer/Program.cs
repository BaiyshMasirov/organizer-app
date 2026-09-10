using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OfficeOpenXml;
using organaizer.Application;
using organaizer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
ExcelPackage.License.SetNonCommercialOrganization("Finance Flow");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => { options.IdleTimeout = TimeSpan.FromHours(12); options.Cookie.IsEssential = true; });
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<FinanceDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Finance")));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = false;
}).AddEntityFrameworkStores<FinanceDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy);
builder.Services.AddScoped<Dispatcher>();
builder.Services.AddScoped<ActiveCompany>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddHttpClient("nbkr", c => { c.BaseAddress = new Uri("https://www.nbkr.kg/"); c.Timeout = TimeSpan.FromSeconds(30); });
builder.Services.AddScoped<NbkrRateService>();
builder.Services.AddHostedService<NbkrRateSyncWorker>();
builder.Services.AddHttpClient("cbr", c => { c.BaseAddress = new Uri("https://www.cbr.ru/"); c.Timeout = TimeSpan.FromSeconds(30); });
builder.Services.AddScoped<CbrRateService>();
builder.Services.AddHostedService<CbrRateSyncWorker>();
builder.Services.AddScoped<ICommandHandler<CreateOperationCommand, Guid>, CreateOperationHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateOperationCommand, bool>, UpdateOperationHandler>();
builder.Services.AddScoped<ICommandHandler<CancelOperationCommand, bool>, CancelOperationHandler>();
builder.Services.AddScoped<ICommandHandler<ReactivateOperationCommand, bool>, ReactivateOperationHandler>();
builder.Services.AddScoped<ICommandHandler<CompleteOperationCommand, bool>, CompleteOperationHandler>();
builder.Services.AddScoped<ICommandHandler<AddSettlementCommand, bool>, AddSettlementHandler>();
builder.Services.AddScoped<IQueryHandler<DashboardQuery, DashboardDto>, DashboardHandler>();
builder.Services.AddScoped<IQueryHandler<MonthlyReportQuery, MonthlyReportDto>, MonthlyReportHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await SeedData.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(), scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    await HistoricalDataImporter.ImportAsync(db,builder.Configuration["HistoricalImportPath"]);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/Company/Select") &&
        !context.Request.Path.StartsWithSegments("/Account/Logout") &&
        string.IsNullOrWhiteSpace(context.Session.GetString(ActiveCompany.SessionKey)))
    {
        context.Response.Redirect("/Company/Select"); return;
    }
    await next();
});

app.UseMiddleware<PermissionMiddleware>();

app.MapRazorPages();

app.Run();
