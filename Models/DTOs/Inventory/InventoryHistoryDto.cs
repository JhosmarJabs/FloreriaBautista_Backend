namespace FloreriaBautista.Models.DTOs.Inventory;

public class InventoryHistoryDto
{
    public List<DailyHistoryDto> Diario { get; set; } = [];
    public List<WeeklyHistoryDto> Semanal { get; set; } = [];
    public List<MonthlyHistoryDto> Mensual { get; set; } = [];
}

public class DailyHistoryDto
{
    public string Date { get; set; } = string.Empty;
    public int Stock { get; set; }
    public string? Nota { get; set; }
    public int Consumed { get; set; }
}

public class WeeklyHistoryDto
{
    public string Label { get; set; } = string.Empty;
    public string Week { get; set; } = string.Empty; // e.g. 2026-W16
    public int Consumed { get; set; }
    public int Restock { get; set; }
    public int Merma { get; set; }
}

public class MonthlyHistoryDto
{
    public string Month { get; set; } = string.Empty; // e.g. 2026-04
    public string Label { get; set; } = string.Empty; // e.g. Abril 2026
    public int TotalConsumed { get; set; }
    public decimal AvgWeekly { get; set; }
    public int TotalRestock { get; set; }
}
