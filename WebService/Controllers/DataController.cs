using Microsoft.AspNetCore.Mvc;
using WebService.Models;
using WebService.Services;

namespace WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly DynamicDataService _dynamicDataService;

    public DataController(DynamicDataService dynamicDataService)
    {
        _dynamicDataService = dynamicDataService;
    }

    [HttpPost]
    public async Task<IActionResult> SaveData([FromBody] DynamicDataRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TableName) || request.Rows == null || request.Rows.Count == 0)
        {
            return BadRequest("Geçersiz veri.");
        }

        await _dynamicDataService.SaveDynamicRowsAsync(
    request.TableName,
    request.Rows,
    request.DefinitionId
        );

        return Ok(new
        {
            message = "Veri başarıyla kaydedildi.",
            tableName = request.TableName,
            rowCount = request.Rows.Count
        });
    }
}