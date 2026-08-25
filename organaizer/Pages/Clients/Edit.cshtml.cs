using System.ComponentModel.DataAnnotations; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using Microsoft.EntityFrameworkCore; using organaizer.Infrastructure;
namespace organaizer.Pages.Clients;
public sealed class EditModel(FinanceDbContext db):PageModel
{
 [BindProperty] public InputModel Input{get;set;}=new(); public sealed class InputModel{public Guid Id{get;set;}[Required,StringLength(180)]public string Name{get;set;}="";[StringLength(80)]public string? Code{get;set;}[StringLength(500)]public string? Note{get;set;}public bool IsActive{get;set;}}
 public async Task<IActionResult> OnGetAsync(Guid id){var x=await db.Counterparties.FindAsync(id);if(x is null)return NotFound();Input=new(){Id=x.Id,Name=x.Name,Code=x.Code,Note=x.Note,IsActive=x.IsActive};return Page();}public async Task<IActionResult> OnPostAsync(){if(!ModelState.IsValid)return Page();var x=await db.Counterparties.FindAsync(Input.Id);if(x is null)return NotFound();x.Name=Input.Name.Trim();x.Code=Input.Code?.Trim();x.Note=Input.Note?.Trim();x.IsActive=Input.IsActive;await db.SaveChangesAsync();return RedirectToPage("Index",new{companyId=x.CompanyId});}
}
