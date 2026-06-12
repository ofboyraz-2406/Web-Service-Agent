namespace WebService.Models;

public class Definition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string MethodName { get; set; } = "";
    public int IsActive { get; set; } 
    public string TableName { get; set; } = "";
    public int? MainDefinitionId { get; set; }
    public string? ResponseArea { get; set; }
    public string? PassTo { get; set; }
    public string? PassKey { get; set; }
    public string? RequestBody { get; set; }
    public int TruncateInsert { get; set; }
    public string? ParseType { get; set; }
    public string? ColumnMap { get; set; }




}