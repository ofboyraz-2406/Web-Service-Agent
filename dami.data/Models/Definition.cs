namespace Dami.Data.Models;

public class Definition
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string ServiceUrl { get; set; } = null!;

    public string MethodName { get; set; } = null!;

    public string ServiceType { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}