using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebService.Data;
using WebService.Models;

namespace WebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DefinitionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DefinitionsController(AppDbContext db)
        {
            _db = db;
        }


        // URL: http://localhost:5244/api/definitions
        [HttpGet]
        public async Task<IActionResult> GetActiveDefinitions()
        {
            var list = await _db.Definitions
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            return Ok(list);
        }

        // URL: http://localhost:5244/api/definitions/due
        [HttpGet("due")]
        public async Task<IActionResult> GetDue()
        {
            var dueItems = await _db.Definitions
                .Where(x => x.IsActive == 1)
                .ToListAsync();
            return Ok(dueItems);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Definition def)
        {
            def.IsActive = 1;
            _db.Definitions.Add(def);
            await _db.SaveChangesAsync();
            return Ok(def);
        }

        // URL: http://localhost:5244/api/definitions/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Definition updated)
        {
            var existing = await _db.Definitions.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = updated.Name;
            existing.Endpoint = updated.Endpoint;
            existing.MethodName = updated.MethodName;
            existing.IsActive = updated.IsActive;
            existing.ServiceType = updated.ServiceType;
            existing.TableName = updated.TableName;
            existing.MainDefinitionId = updated.MainDefinitionId;
            existing.ResponseArea = updated.ResponseArea;
            existing.PassTo = updated.PassTo;
            existing.PassKey = updated.PassKey;
            existing.RequestBody = updated.RequestBody;
            existing.TruncateInsert = updated.TruncateInsert;
            existing.ParseType = updated.ParseType;       
            existing.ColumnMap = updated.ColumnMap; 


            await _db.SaveChangesAsync();
            return Ok(new { message = "Güncellendi" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var def = await _db.Definitions.FindAsync(id);
            if (def == null) return NotFound();

            def.IsActive = 0;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Servis silindi." });
        }


        [HttpPut("{id}/start")]
        public async Task<IActionResult> Start(int id)
        {
            var sch = await _db.ScheduleManagers.FirstOrDefaultAsync(x => x.DefinitionId == id);
            if (sch == null) return Ok(); 
            sch.IsProcessing = 1;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> Complete(int id)
        {
            var sch = await _db.ScheduleManagers.FirstOrDefaultAsync(x => x.DefinitionId == id);
            if (sch == null) return Ok(); 
            sch.IsProcessing = 0;
            sch.ProcessedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id}/reset")]
        public async Task<IActionResult> Reset(int id)
        {
            var sch = await _db.ScheduleManagers.FirstOrDefaultAsync(x => x.DefinitionId == id);
            if (sch == null) return Ok(); 
            sch.IsProcessing = 0;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
    