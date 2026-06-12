using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Background.service.Models;

public class DynamicDataRequest
{
    public string TableName { get; set; } = "";
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}
