using System.ComponentModel.DataAnnotations; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using Microsoft.EntityFrameworkCore; using organaizer.Domain; using organaizer.Infrastructure;
namespace organaizer.Pages.Currencies;
public sealed class IndexModel(FinanceDbContext db):PageModel
{
 public List<Currency> Items{get;private set;}=[];[BindProperty]public InputModel Input{get;set;}=new();public sealed class InputModel{[Required,StringLength(5)]public string Code{get;set;}="";[Required,StringLength(80)]public string Name{get;set;}="";[StringLength(8)]public string Symbol{get;set;}="";[Range(0,8)]public int Precision{get;set;}=2;}
 public async Task OnGetAsync()=>await Load();public async Task<IActionResult> OnPostAsync(){Input.Code=Input.Code.Trim().ToUpperInvariant();if(await db.Currencies.AnyAsync(x=>x.Code==Input.Code))ModelState.AddModelError("Input.Code","Такая валюта уже есть");if(!ModelState.IsValid){await Load();return Page();}db.Currencies.Add(new Currency{Code=Input.Code,Name=Input.Name.Trim(),Symbol=Input.Symbol.Trim(),Precision=Input.Precision});await db.SaveChangesAsync();return RedirectToPage();}async Task Load()=>Items=await db.Currencies.AsNoTracking().OrderBy(x=>x.Code).ToListAsync();
}
