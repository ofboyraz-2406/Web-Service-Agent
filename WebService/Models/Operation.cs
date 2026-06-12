namespace WebService.Models;

public class Operation
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string OperationName { get; set; } = "";
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string Status { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}