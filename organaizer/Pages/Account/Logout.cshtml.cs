using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace organaizer.Pages.Account;
public sealed class LogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync() { await signInManager.SignOutAsync(); return RedirectToPage("/Account/Login"); }
}
