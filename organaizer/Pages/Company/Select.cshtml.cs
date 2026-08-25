using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.CompanySelection;
public sealed class SelectModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    public List<Domain.Company> Companies { get; private set; } = [];
    public string? ReturnUrl { get; private set; }
    public async Task OnGetAsync(string? returnUrl = null) { ReturnUrl = returnUrl; Companies = await db.Companies.AsNoTracking().OrderBy(x => x.Kind).ToListAsync(); }
    public async Task<IActionResult> OnPostAsync(Guid companyId, string? returnUrl = null)
    {
        if (!await db.Companies.AnyAsync(x => x.Id == companyId)) return BadRequest();
        active.Select(companyId);
        return LocalRedirect(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
