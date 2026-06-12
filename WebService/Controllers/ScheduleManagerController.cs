using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebService.Data;
using WebService.Models;

namespace WebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScheduleManagerController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ScheduleManagerController(AppDbContext db) { _db = db; }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _db.ScheduleManagers.ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ScheduleManager sch)
        {
            _db.ScheduleManagers.Add(sch);
            await _db.SaveChangesAsync();
            return Ok(sch);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ScheduleManager updated)
        {
            var existing = await _db.ScheduleManagers.FindAsync(id);
            if (existing == null) return NotFound();
            existing.DefinitionId = updated.DefinitionId;
            existing.ScheduledPeriod = updated.ScheduledPeriod;
            existing.PeriodType = updated.PeriodType;
            existing.StartTime = updated.StartTime;
            existing.EndTime = updated.EndTime;
            existing.ScheduledTime = updated.ScheduledTime;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Zamanlama Güncellendi" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var sch = await _db.ScheduleManagers.FindAsync(id);
            if (sch == null) return NotFound();
            _db.ScheduleManagers.Remove(sch);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Zamanlama Silindi" });
        }
    }
}