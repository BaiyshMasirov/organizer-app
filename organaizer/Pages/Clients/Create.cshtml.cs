using System.ComponentModel.DataAnnotations; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore; using organaizer.Domain; using organaizer.Infrastructure;
namespace organaizer.Pages.Clients;
public sealed class CreateModel(FinanceDbContext db):PageModel
{
 [BindProperty] public InputModel Input{get;set;}=new(); public List<SelectListItem> Companies{get;private set;}=[];
 public sealed class InputModel{[Required]public Guid CompanyId{get;set;}[Required,StringLength(180)]public string Name{get;set;}="";[StringLength(80)]public string? Code{get;set;}[StringLength(500)]public string? Note{get;set;}}
 public async Task OnGetAsync()=>await Load(); public async Task<IActionResult> OnPostAsync(){if(await db.Counterparties.AnyAsync(x=>x.CompanyId==Input.CompanyId&&x.Name.ToLower()==Input.Name.Trim().ToLower()))ModelState.AddModelError("Input.Name","Клиент с таким названием уже существует");if(!ModelState.IsValid){await Load();return Page();}db.Counterparties.Add(new Counterparty{Id=Guid.NewGuid(),CompanyId=Input.CompanyId,Name=Input.Name.Trim(),Code=Input.Code?.Trim(),Note=Input.Note?.Trim(),Kind=CounterpartyKind.Client});await db.SaveChangesAsync();return RedirectToPage("Index",new{companyId=Input.CompanyId});} async Task Load()=>Companies=await db.Companies.AsNoTracking().Select(x=>new SelectListItem(x.Name,x.Id.ToString())).ToListAsync();
}
