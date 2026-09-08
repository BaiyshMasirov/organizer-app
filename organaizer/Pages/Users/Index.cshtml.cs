using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using organaizer.Infrastructure;

namespace organaizer.Pages.Users;

public sealed class IndexModel(UserManager<IdentityUser> users) : PageModel
{
    public sealed record UserRow(string Id, string UserName, bool IsActive, bool IsSuperAdmin, HashSet<string> Permissions);
    public List<UserRow> Items { get; private set; } = [];
    public IReadOnlyList<PermissionSection> Sections => AppPermissions.Sections;

    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync(string username, string password, bool isActive, string[] permissions)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username)) ModelState.AddModelError(string.Empty, "Укажите логин.");
        if (password.Length < 8) ModelState.AddModelError(string.Empty, "Пароль должен содержать минимум 8 символов.");
        if (await users.FindByNameAsync(username) is not null) ModelState.AddModelError(string.Empty, "Пользователь с таким логином уже существует.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        var user = new IdentityUser { UserName = username, Email = $"{username}@local", EmailConfirmed = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await LoadAsync(); return Page();
        }
        await SetPermissionsAsync(user, permissions);
        if (!isActive) await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        Message = $"Пользователь {username} создан.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(string userId, bool isActive, string? newPassword, string[] permissions)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, AppPermissions.SuperAdminRole))
        {
            Message = "Права супер-администратора фиксированы и не ограничиваются.";
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 8) { ModelState.AddModelError(string.Empty, "Новый пароль должен содержать минимум 8 символов."); await LoadAsync(); return Page(); }
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, token, newPassword);
            if (!reset.Succeeded)
            {
                foreach (var error in reset.Errors) ModelState.AddModelError(string.Empty, error.Description);
                await LoadAsync(); return Page();
            }
        }

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, isActive ? null : DateTimeOffset.MaxValue);
        await SetPermissionsAsync(user, permissions);
        await users.UpdateSecurityStampAsync(user);
        Message = $"Настройки пользователя {user.UserName} сохранены.";
        return RedirectToPage();
    }

    private async Task SetPermissionsAsync(IdentityUser user, IEnumerable<string> requested)
    {
        var allowed = requested.Intersect(AppPermissions.All, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var section in AppPermissions.Sections)
            if ((section.CanCreate && allowed.Contains(section.Create)) || (section.CanEdit && allowed.Contains(section.Edit))) allowed.Add(section.View);
        var existing = (await users.GetClaimsAsync(user)).Where(x => x.Type == AppPermissions.ClaimType).ToList();
        foreach (var claim in existing) await users.RemoveClaimAsync(user, claim);
        foreach (var permission in allowed) await users.AddClaimAsync(user, new Claim(AppPermissions.ClaimType, permission));
    }

    private async Task LoadAsync()
    {
        Items = [];
        foreach (var user in users.Users.OrderBy(x => x.UserName).ToList())
        {
            var claims = (await users.GetClaimsAsync(user)).Where(x => x.Type == AppPermissions.ClaimType).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Items.Add(new UserRow(user.Id, user.UserName ?? "—", !(user.LockoutEnd > DateTimeOffset.UtcNow), await users.IsInRoleAsync(user, AppPermissions.SuperAdminRole), claims));
        }
    }
}
