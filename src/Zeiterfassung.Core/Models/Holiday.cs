namespace Zeiterfassung.Core.Models;

public class Holiday
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = null!;
    public string? State { get; set; }
}
