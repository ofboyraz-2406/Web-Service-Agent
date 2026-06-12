using Microsoft.AspNetCore.Mvc;
using WebService.Data;
using WebService.Models;

namespace WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public OperationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOperation([FromBody] OperationRequest request)
    {
        if (request == null)
        {
            return BadRequest("Geçersiz istek.");
        }

        var operation = new Operation
        {
            DefinitionId = request.DefinitionId,
            OperationName = request.OperationName,
            RequestPayload = request.RequestPayload,
            ResponsePayload = request.ResponsePayload,
            Status = request.Status,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _db.Operations.Add(operation);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Operation kaydedildi.",
            operation.Id
        });
    }
}