namespace WebService.Models;

public class OperationRequest
{
    public int DefinitionId { get; set; }
    public string OperationName { get; set; } = "";
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string Status { get; set; } = "";
}