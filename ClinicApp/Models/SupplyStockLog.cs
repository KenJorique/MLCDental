using SQLite;

namespace ClinicApp.Models;

[Table("SupplyStockLog")]
public class SupplyStockLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SupplyItemId { get; set; }

    /// <summary>"Restocked" | "Used" | "Adjusted"</summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>Positive = added, negative = consumed.</summary>
    public int ChangeInPieces { get; set; }

    /// <summary>Snapshot of stock level (in pieces) after this transaction.</summary>
    public int StockAfterChange { get; set; }

    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
