using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.OrientExchangeRates;

public sealed class IndexModel(FinanceDbContext db, NbkrRateService rates, ActiveCompany active) : PageModel
{
    public List<NbkrExchangeRate> Items { get; private set; } = [];
    [TempData] public string? Message { get; set; }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsOrientAsync()) return NotFound();
        Items = await db.NbkrExchangeRates.AsNoTracking().OrderByDescending(x => x.EffectiveAt).ThenBy(x => x.Currency).Take(250).ToListAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostSyncAsync()
    {
        if (!await IsOrientAsync()) return NotFound();
        var changed = await rates.SyncAsync(HttpContext.RequestAborted);
        Message = changed == 0 ? "Курсы уже актуальны." : $"Курсы НБКР обновлены: {changed}.";
        return RedirectToPage();
    }
    async Task<bool> IsOrientAsync() => await db.Companies.AsNoTracking().AnyAsync(x => x.Id == active.RequiredId && x.Kind == CompanyKind.Broker);
}
