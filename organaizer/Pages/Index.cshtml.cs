using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages;
public sealed class IndexModel(Dispatcher dispatcher, FinanceDbContext db, ActiveCompany active) : PageModel
{
    public DashboardDto Dashboard { get; private set; } = null!;
    public List<Domain.Company> Companies { get; private set; } = [];
    public Guid? CompanyId { get; private set; }
    public async Task OnGetAsync(Guid? companyId, int? year, int? month)
    {
        CompanyId=active.RequiredId; Companies=await db.Companies.AsNoTracking().Where(x=>x.Id==active.RequiredId).ToListAsync();
        var now=DateTimeOffset.UtcNow; var from=new DateTimeOffset(year??now.Year,month??now.Month,1,0,0,0,TimeSpan.Zero);
        Dashboard=await dispatcher.Query(new DashboardQuery(active.RequiredId,from,from.AddMonths(1)));
    }
}
