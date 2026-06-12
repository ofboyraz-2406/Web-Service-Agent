using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Background.Service.Models;
public class ScheduleManager
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string ScheduledTime { get; set; } = "";
    public int IsProcessing { get; set; }
    public string? ProcessedAt { get; set; }
    public int ScheduledPeriod { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? PeriodType { get; set; }
}
