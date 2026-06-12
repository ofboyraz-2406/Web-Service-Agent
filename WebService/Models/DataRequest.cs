namespace WebService.Models;

public class DynamicDataRequest
{
    public int DefinitionId { get; set; }
    public string TableName { get; set; } = "";
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}