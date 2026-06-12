using Dami.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dami.RestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet("top5")]
    public async Task<IActionResult> GetTop5()
    {
        var customers = await _db.Customers
            .OrderBy(x => x.Id)
            .Take(5)
            .ToListAsync();

        return Ok(customers);
    }
}