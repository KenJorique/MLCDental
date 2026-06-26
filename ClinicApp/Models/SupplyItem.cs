using SQLite;

namespace ClinicApp.Models;

[Table("SupplyItem")]
public class SupplyItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = "Per Piece";

    public int PiecesPerUnit { get; set; } = 1;

    public int QuantityInPieces { get; set; } = 0;

    public bool HasExpiration { get; set; } = false;

    public string ExpirationDate { get; set; } = string.Empty;

    public int MinimumStockPieces { get; set; } = 10;

    public string AddedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    // True = hidden from list but kept in DB (soft delete)
    public bool IsDeleted { get; set; } = false;

    [Ignore]
    public bool IsLowStock => QuantityInPieces <= MinimumStockPieces;

    [Ignore]
    public bool IsOutOfStock => QuantityInPieces <= 0;

    [Ignore]
    public string QuantityDisplay => $"{QuantityInPieces} pcs";
}
