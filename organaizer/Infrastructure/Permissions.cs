using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace organaizer.Infrastructure;

public sealed record PermissionSection(string Key, string Name, bool CanCreate = true, bool CanEdit = true)
{
    public string View => $"{Key}.View";
    public string Create => $"{Key}.Create";
    public string Edit => $"{Key}.Edit";
}

public static class AppPermissions
{
    public const string ClaimType = "finance_permission";
    public const string SuperAdminRole = "SuperAdmin";

    public static readonly PermissionSection Dashboard = new("Dashboard", "Обзор", false, false);
    public static readonly PermissionSection Operations = new("Operations", "Операции");
    public static readonly PermissionSection Balance = new("Balance", "Баланс");
    public static readonly PermissionSection Clients = new("Clients", "Клиенты");
    public static readonly PermissionSection Currencies = new("Currencies", "Валюты", true, false);
    public static readonly PermissionSection Institutions = new("Institutions", "Банки и каналы", true, false);
    public static readonly PermissionSection Expenses = new("Expenses", "Расходы");
    public static readonly PermissionSection ExchangeRates = new("ExchangeRates", "Курсы валют");
    public static readonly PermissionSection OrientRates = new("OrientRates", "Курсы НБКР", false, true);
    public static readonly PermissionSection Reports = new("Reports", "Промежуточный отчёт", false, false);
    public static readonly PermissionSection Analytics = new("Analytics", "Диаграммы и итоги", false, false);
    public static readonly PermissionSection Executive = new("Executive", "Дашборд руководителя", false, false);

    public static readonly IReadOnlyList<PermissionSection> Sections =
    [Dashboard, Operations, Balance, Clients, Currencies, Institutions, Expenses, ExchangeRates, OrientRates, Reports, Analytics, Executive];

    public static readonly IReadOnlyList<string> All = Sections
        .SelectMany(x => new[] { x.View }.Concat(x.CanCreate ? [x.Create] : []).Concat(x.CanEdit ? [x.Edit] : []))
        .ToList();
}

public sealed class PermissionService(FinanceDbContext db)
{
    private string? _userId;
    private HashSet<string>? _permissions;
    private bool _isSuperAdmin;

    public async Task<bool> IsSuperAdminAsync(ClaimsPrincipal principal)
    {
        await LoadAsync(principal);
        return _isSuperAdmin;
    }

    public async Task<bool> HasAsync(ClaimsPrincipal principal, string permission)
    {
        await LoadAsync(principal);
        return _isSuperAdmin || _permissions!.Contains(permission);
    }

    private async Task LoadAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (_permissions is not null && id == _userId) return;
        _userId = id;
        _permissions = [];
        _isSuperAdmin = false;
        if (string.IsNullOrWhiteSpace(id)) return;
        if (await db.Users.AsNoTracking().AnyAsync(x => x.Id == id && x.LockoutEnd > DateTimeOffset.UtcNow)) return;
        _permissions = (await db.UserClaims.AsNoTracking()
            .Where(x => x.UserId == id && x.ClaimType == AppPermissions.ClaimType && x.ClaimValue != null)
            .Select(x => x.ClaimValue!).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _isSuperAdmin = await (from ur in db.UserRoles.AsNoTracking()
                               join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
                               where ur.UserId == id && role.Name == AppPermissions.SuperAdminRole
                               select ur).AnyAsync();
    }
}

public sealed class PermissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PermissionService permissions)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/users"))
        {
            if (!await permissions.IsSuperAdminAsync(context.User)) { context.Response.Redirect("/Account/AccessDenied"); return; }
            await next(context); return;
        }
        var required = await RequiredPermissionAsync(context, path);
        if (required is not null && !await permissions.HasAsync(context.User, required))
        {
            context.Response.Redirect("/Account/AccessDenied"); return;
        }
        await next(context);
    }

    private static async Task<string?> RequiredPermissionAsync(HttpContext context, string path)
    {
        if (path is "/" or "/index") return AppPermissions.Dashboard.View;
        if (path.StartsWith("/executive")) return AppPermissions.Executive.View;
        if (path.StartsWith("/reports")) return AppPermissions.Reports.View;
        if (path.StartsWith("/orientexchangerates")) return context.Request.Method == "POST" ? AppPermissions.OrientRates.Edit : AppPermissions.OrientRates.View;
        if (path.StartsWith("/operations"))
        {
            if (path.Contains("/create")) return AppPermissions.Operations.Create;
            if (path.Contains("/edit") || context.Request.Method == "POST") return AppPermissions.Operations.Edit;
            return AppPermissions.Operations.View;
        }
        if (path.StartsWith("/clients"))
        {
            if (path.Contains("/create")) return AppPermissions.Clients.Create;
            if (path.Contains("/edit")) return AppPermissions.Clients.Edit;
            return AppPermissions.Clients.View;
        }
        if (path.StartsWith("/expenses"))
        {
            if (path.Contains("/create")) return AppPermissions.Expenses.Create;
            if (path.Contains("/edit")) return AppPermissions.Expenses.Edit;
            return AppPermissions.Expenses.View;
        }
        if (path.StartsWith("/balance"))
        {
            if (context.Request.Method != "POST") return AppPermissions.Balance.View;
            var handler = context.Request.Query["handler"].ToString();
            return string.IsNullOrWhiteSpace(handler) ? AppPermissions.Balance.Create : AppPermissions.Balance.Edit;
        }
        if (path.StartsWith("/currencies")) return context.Request.Method == "POST" ? AppPermissions.Currencies.Create : AppPermissions.Currencies.View;
        if (path.StartsWith("/institutions")) return context.Request.Method == "POST" ? AppPermissions.Institutions.Create : AppPermissions.Institutions.View;
        if (path.StartsWith("/exchangerates"))
        {
            if (context.Request.Method != "POST") return context.Request.Query.ContainsKey("edit") ? AppPermissions.ExchangeRates.Edit : AppPermissions.ExchangeRates.View;
            if (string.Equals(context.Request.Query["handler"], "Delete", StringComparison.OrdinalIgnoreCase)) return AppPermissions.ExchangeRates.Edit;
            if (!context.Request.HasFormContentType) return AppPermissions.ExchangeRates.Edit;
            var form = await context.Request.ReadFormAsync();
            return !Guid.TryParse(form["Input.Id"], out var id) || id == Guid.Empty
                ? AppPermissions.ExchangeRates.Create : AppPermissions.ExchangeRates.Edit;
        }
        return null;
    }
}
