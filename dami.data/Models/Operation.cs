namespace Dami.Data.Models;

public class Operation
{
    public int Id { get; set; }

    public int DefinitionId { get; set; }
    public Definition? Definition { get; set; }

    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorMessage { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}