using System.ComponentModel.DataAnnotations;using Microsoft.AspNetCore.Mvc;using Microsoft.AspNetCore.Mvc.RazorPages;using Microsoft.EntityFrameworkCore;using organaizer.Domain;using organaizer.Infrastructure;
namespace organaizer.Pages.Institutions;
public sealed class IndexModel(FinanceDbContext db):PageModel
{
 public List<FinancialInstitution>Items{get;private set;}=[];[BindProperty]public InputModel Input{get;set;}=new();public sealed class InputModel{[Required,StringLength(160)]public string Name{get;set;}="";public InstitutionKind Kind{get;set;}=InstitutionKind.Bank;[StringLength(300)]public string?Note{get;set;}}
 public async Task OnGetAsync()=>await Load();public async Task<IActionResult>OnPostAsync(){if(await db.FinancialInstitutions.AnyAsync(x=>x.Name.ToLower()==Input.Name.Trim().ToLower()))ModelState.AddModelError("Input.Name","Такая запись уже существует");if(!ModelState.IsValid){await Load();return Page();}db.FinancialInstitutions.Add(new FinancialInstitution{Id=Guid.NewGuid(),Name=Input.Name.Trim(),Kind=Input.Kind,Note=Input.Note?.Trim()});await db.SaveChangesAsync();return RedirectToPage();}async Task Load()=>Items=await db.FinancialInstitutions.AsNoTracking().OrderBy(x=>x.Kind).ThenBy(x=>x.Name).ToListAsync();
}
