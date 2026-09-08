using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using organaizer.Infrastructure;

namespace organaizer.Pages.Users;

public sealed class EditUserModel(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles) : PageModel
{
    [BindProperty] public string Id { get; set; } = "";
    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public bool IsActive { get; set; }
    [BindProperty] public string[] SelectedRoles { get; set; } = [];
    public List<IdentityRole> AvailableRoles { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, AppPermissions.SuperAdminRole)) return RedirectToPage("Index", new { tab = "users" });
        Id = user.Id; UserName = user.UserName ?? ""; IsActive = !(user.LockoutEnd > DateTimeOffset.UtcNow);
        SelectedRoles = (await users.GetRolesAsync(user)).Where(x => x != AppPermissions.SuperAdminRole).ToArray();
        LoadRoles(); return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.FindByIdAsync(Id);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, AppPermissions.SuperAdminRole)) return Forbid();
        UserName = UserName.Trim();
        if (string.IsNullOrWhiteSpace(UserName)) ModelState.AddModelError(nameof(UserName), "Укажите логин.");
        var duplicate = await users.FindByNameAsync(UserName);
        if (duplicate is not null && duplicate.Id != user.Id) ModelState.AddModelError(nameof(UserName), "Этот логин уже занят.");
        if (!string.IsNullOrWhiteSpace(NewPassword) && NewPassword.Length < 8) ModelState.AddModelError(nameof(NewPassword), "Пароль должен содержать минимум 8 символов.");
        if (!ModelState.IsValid) { LoadRoles(); return Page(); }

        user.UserName = UserName; user.Email = $"{UserName}@local";
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded) { foreach (var error in update.Errors) ModelState.AddModelError(string.Empty, error.Description); LoadRoles(); return Page(); }
        if (!string.IsNullOrWhiteSpace(NewPassword))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, token, NewPassword);
            if (!reset.Succeeded) { foreach (var error in reset.Errors) ModelState.AddModelError(string.Empty, error.Description); LoadRoles(); return Page(); }
        }
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, IsActive ? null : DateTimeOffset.MaxValue);
        var available = roles.Roles.Where(x => x.Name != AppPermissions.SuperAdminRole).Select(x => x.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requested = SelectedRoles.Where(available.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = (await users.GetRolesAsync(user)).Where(x => x != AppPermissions.SuperAdminRole).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (current.Except(requested).Any()) await users.RemoveFromRolesAsync(user, current.Except(requested));
        if (requested.Except(current).Any()) await users.AddToRolesAsync(user, requested.Except(current));
        var legacyClaims = (await users.GetClaimsAsync(user)).Where(x => x.Type == AppPermissions.ClaimType).ToList();
        foreach (var claim in legacyClaims) await users.RemoveClaimAsync(user, claim);
        await users.UpdateSecurityStampAsync(user);
        TempData["Message"] = $"Пользователь {UserName} обновлён.";
        return RedirectToPage("Index", new { tab = "users" });
    }

    private void LoadRoles() => AvailableRoles = roles.Roles.Where(x => x.Name != AppPermissions.SuperAdminRole).OrderBy(x => x.Name).ToList();
}
