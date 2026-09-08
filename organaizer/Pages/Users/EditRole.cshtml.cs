using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using organaizer.Infrastructure;

namespace organaizer.Pages.Users;

public sealed class EditRoleModel(RoleManager<IdentityRole> roles, UserManager<IdentityUser> users) : PageModel
{
    [BindProperty] public string Id { get; set; } = "";
    [BindProperty] public string RoleName { get; set; } = "";
    [BindProperty] public string[] Permissions { get; set; } = [];
    public IReadOnlyList<PermissionSection> Sections => AppPermissions.Sections;
    public HashSet<string> Selected { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var role = await roles.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (role.Name == AppPermissions.SuperAdminRole) return RedirectToPage("Index", new { tab = "roles" });
        Id = role.Id; RoleName = role.Name ?? "";
        Selected = (await roles.GetClaimsAsync(role)).Where(x => x.Type == AppPermissions.ClaimType).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var role = await roles.FindByIdAsync(Id);
        if (role is null) return NotFound();
        if (role.Name == AppPermissions.SuperAdminRole) return Forbid();
        RoleName = RoleName.Trim();
        if (string.IsNullOrWhiteSpace(RoleName)) ModelState.AddModelError(nameof(RoleName), "Укажите наименование роли.");
        var duplicate = await roles.FindByNameAsync(RoleName);
        if (duplicate is not null && duplicate.Id != role.Id) ModelState.AddModelError(nameof(RoleName), "Роль с таким наименованием уже существует.");
        var allowed = Permissions.Intersect(AppPermissions.All, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var section in Sections) if ((section.CanCreate && allowed.Contains(section.Create)) || (section.CanEdit && allowed.Contains(section.Edit))) allowed.Add(section.View);
        Selected = allowed;
        if (!ModelState.IsValid) return Page();
        role.Name = RoleName;
        var update = await roles.UpdateAsync(role);
        if (!update.Succeeded) { foreach (var error in update.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }
        var existing = (await roles.GetClaimsAsync(role)).Where(x => x.Type == AppPermissions.ClaimType).ToList();
        foreach (var claim in existing) await roles.RemoveClaimAsync(role, claim);
        foreach (var permission in allowed) await roles.AddClaimAsync(role, new Claim(AppPermissions.ClaimType, permission));
        TempData["Message"] = $"Роль {RoleName} и её права сохранены.";
        return RedirectToPage("Index", new { tab = "roles" });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var role = await roles.FindByIdAsync(Id);
        if (role is null) return NotFound();
        if (role.Name == AppPermissions.SuperAdminRole) return Forbid();
        if ((await users.GetUsersInRoleAsync(role.Name!)).Count > 0) { ModelState.AddModelError(string.Empty, "Нельзя удалить роль, пока она назначена пользователям."); RoleName = role.Name!; Selected = (await roles.GetClaimsAsync(role)).Where(x => x.Type == AppPermissions.ClaimType).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase); return Page(); }
        await roles.DeleteAsync(role);
        TempData["Message"] = $"Роль {role.Name} удалена.";
        return RedirectToPage("Index", new { tab = "roles" });
    }
}
