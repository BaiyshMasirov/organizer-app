using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;
namespace organaizer.Pages.Clients;
public sealed class IndexModel(FinanceDbContext db,ActiveCompany active):PageModel
{
 public List<Domain.Company> Companies {get;private set;}=[]; public List<Counterparty> Clients {get;private set;}=[]; public Guid? CompanyId {get;private set;} public string? Search {get;private set;}
 public async Task OnGetAsync(Guid? companyId,string? search){CompanyId=active.RequiredId;Search=search;Companies=await db.Companies.AsNoTracking().Where(x=>x.Id==active.RequiredId).ToListAsync();var q=db.Counterparties.AsNoTracking().Where(x=>x.Kind==CounterpartyKind.Client&&x.CompanyId==active.RequiredId);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.ToLower().Contains(search.ToLower()));Clients=await q.OrderBy(x=>x.Name).ToListAsync();}
}
