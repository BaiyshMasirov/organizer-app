using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.ExchangeRates;

public sealed class IndexModel(FinanceDbContext db, CbrRateService cbr, ActiveCompany active) : PageModel
{
    public List<ExchangeRate> Items { get; private set; } = [];
    public SelectList Currencies { get; private set; } = null!;
    public string? Currency { get; private set; }
    public int? Year { get; private set; }
    public int? Month { get; private set; }
    public bool IsLiquidityProvider { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        public Guid Id { get; set; }
        [Required, StringLength(5)] public string Currency { get; set; } = "RUB";
        [Required] public DateTime EffectiveAt { get; set; } = DateTime.Today;
        [Range(typeof(decimal), "0.000000000000001", "9999999999999999")] public decimal RateToUsd { get; set; }
        [StringLength(300)] public string? Note { get; set; }
    }

    public async Task OnGetAsync(string? currency, int? year, int? month, Guid? edit)
    {
        Currency = currency;
        Year = year;
        Month = month;
        if (edit.HasValue)
        {
            var x = await db.ExchangeRates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == edit);
            if (x is not null) Input = new InputModel { Id = x.Id, Currency = x.Currency, EffectiveAt = x.EffectiveAt.DateTime, RateToUsd = x.RateToUsd, Note = x.Note };
        }
        await Load();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Currency = Input.Currency.ToUpperInvariant();
        if (!ModelState.IsValid) { await Load(); return Page(); }
        ExchangeRate x;
        if (Input.Id == Guid.Empty)
        {
            x = new ExchangeRate { Id = Guid.NewGuid(), Currency = Input.Currency, EffectiveAt = new DateTimeOffset(Input.EffectiveAt, TimeSpan.Zero), RateToUsd = Input.RateToUsd, Note = Input.Note?.Trim() };
            db.ExchangeRates.Add(x);
        }
        else
        {
            x = await db.ExchangeRates.SingleAsync(x => x.Id == Input.Id);
            x.Currency = Input.Currency;
            x.EffectiveAt = new DateTimeOffset(Input.EffectiveAt, TimeSpan.Zero);
            x.RateToUsd = Input.RateToUsd;
            x.Note = Input.Note?.Trim();
        }
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncCbrAsync(DateTime? date)
    {
        var kind = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId).Select(x => (CompanyKind?)x.Kind).SingleOrDefaultAsync();
        if (kind != CompanyKind.LiquidityProvider) return Forbid();
        try
        {
            var result = await cbr.SyncRubToUsdAsync(date?.Date ?? DateTime.Today);
            TempData["Success"] = $"Курс ЦБ РФ обновлен: 1 RUB = {result.RubToUsd:N8} USD";
        }
        catch (Exception ex)
        {
            TempData["RateError"] = $"Не удалось получить курс ЦБ РФ: {ex.Message}";
        }
        return RedirectToPage(new { currency = "RUB" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var x = await db.ExchangeRates.SingleOrDefaultAsync(x => x.Id == id);
        if (x is not null) { db.ExchangeRates.Remove(x); await db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    private async Task Load()
    {
        IsLiquidityProvider = await db.Companies.AsNoTracking().AnyAsync(x => x.Id == active.RequiredId && x.Kind == CompanyKind.LiquidityProvider);
        Currencies = new SelectList(await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(), "Code", "Code", Input.Currency);
        var q = db.ExchangeRates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Currency)) q = q.Where(x => x.Currency == Currency);
        if (Year.HasValue) q = q.Where(x => x.EffectiveAt.Year == Year);
        if (Month is >= 1 and <= 12) q = q.Where(x => x.EffectiveAt.Month == Month);
        Items = await q.OrderByDescending(x => x.EffectiveAt).ThenBy(x => x.Currency).Take(500).ToListAsync();
    }
}
