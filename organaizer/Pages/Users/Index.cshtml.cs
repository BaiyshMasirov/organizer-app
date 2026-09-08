using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using organaizer.Infrastructure;

namespace organaizer.Pages.Users;

public sealed class IndexModel(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles) : PageModel
{
    public sealed record UserRow(string Id, string UserName, bool IsActive, bool IsSuperAdmin, List<string> Roles);
    public sealed record RoleRow(string Id, string Name, int UserCount, int PermissionCount, bool IsSystem);
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [TempData] public string? Message { get; set; }
    public List<UserRow> UserItems { get; private set; } = [];
    public List<RoleRow> RoleItems { get; private set; } = [];
    public List<IdentityRole> AssignableRoles { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateUserAsync(string? username, string? password, bool isActive, string[]? selectedRoles)
    {
        username = username?.Trim() ?? "";
        password ??= "";
        selectedRoles ??= [];
        if (string.IsNullOrWhiteSpace(username)) ModelState.AddModelError(string.Empty, "Укажите логин.");
        if (password.Length < 8) ModelState.AddModelError(string.Empty, "Пароль должен содержать минимум 8 символов.");
        if (await users.FindByNameAsync(username) is not null) ModelState.AddModelError(string.Empty, "Пользователь с таким логином уже существует.");
        if (!ModelState.IsValid) { Tab = "users"; await LoadAsync(); return Page(); }
        var user = new IdentityUser { UserName = username, Email = $"{username}@local", EmailConfirmed = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); Tab = "users"; await LoadAsync(); return Page(); }
        var allowed = roles.Roles.Where(x => x.Name != AppPermissions.SuperAdminRole && selectedRoles.Contains(x.Name!)).Select(x => x.Name!).ToList();
        if (allowed.Count > 0) await users.AddToRolesAsync(user, allowed);
        if (!isActive) await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        Message = $"Пользователь {username} создан.";
        return RedirectToPage(new { tab = "users" });
    }

    public async Task<IActionResult> OnPostCreateRoleAsync(string? roleName)
    {
        roleName = roleName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(roleName)) ModelState.AddModelError(string.Empty, "Укажите наименование роли.");
        if (string.Equals(roleName, AppPermissions.SuperAdminRole, StringComparison.OrdinalIgnoreCase) || await roles.RoleExistsAsync(roleName)) ModelState.AddModelError(string.Empty, "Роль с таким наименованием уже существует.");
        if (!ModelState.IsValid) { Tab = "roles"; await LoadAsync(); return Page(); }
        var result = await roles.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); Tab = "roles"; await LoadAsync(); return Page(); }
        var role = await roles.FindByNameAsync(roleName);
        Message = $"Роль {roleName} создана.";
        return RedirectToPage("EditRole", new { id = role!.Id });
    }

    private async Task LoadAsync()
    {
        Tab = string.Equals(Tab, "roles", StringComparison.OrdinalIgnoreCase) ? "roles" : "users";
        var search = Search?.Trim();
        AssignableRoles = roles.Roles.Where(x => x.Name != AppPermissions.SuperAdminRole).OrderBy(x => x.Name).ToList();
        foreach (var user in users.Users.OrderBy(x => x.UserName).ToList())
        {
            if (!string.IsNullOrWhiteSpace(search) && !(user.UserName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            var assigned = (await users.GetRolesAsync(user)).OrderBy(x => x).ToList();
            UserItems.Add(new UserRow(user.Id, user.UserName ?? "—", !(user.LockoutEnd > DateTimeOffset.UtcNow), assigned.Contains(AppPermissions.SuperAdminRole), assigned));
        }
        foreach (var role in roles.Roles.OrderBy(x => x.Name).ToList())
        {
            if (!string.IsNullOrWhiteSpace(search) && !(role.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            var members = await users.GetUsersInRoleAsync(role.Name!);
            var claims = await roles.GetClaimsAsync(role);
            RoleItems.Add(new RoleRow(role.Id, role.Name ?? "—", members.Count, claims.Count(x => x.Type == AppPermissions.ClaimType), role.Name == AppPermissions.SuperAdminRole));
        }
    }
}
