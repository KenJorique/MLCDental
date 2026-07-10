using SQLite;

namespace ClinicApp.Models;

[Table("SupplyItem")]
public class SupplyItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Per Piece | Per Pack | Per Box | Per Kit</summary>
    public string Unit { get; set; } = "Per Piece";

    /// <summary>How many pieces are in one unit. Always 1 when Unit is Per Piece.</summary>
    public int PiecesPerUnit { get; set; } = 1;

    public int QuantityInPieces { get; set; } = 0;

    /// Optional: size or variant label (e.g. "Small", "Large", "250mg")
    public string SizeVariant { get; set; } = string.Empty;

    /// <summary>Whether the item has an expiration date to track.
    public bool HasExpiration { get; set; } = false;

    public string ExpirationDate { get; set; } = string.Empty;

    public int MinimumStockPieces { get; set; } = 10;

    public string AddedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    // ── Computed (not stored) ─────────────────────────────────────
    [Ignore]
    public bool IsLowStock => QuantityInPieces <= MinimumStockPieces;

    [Ignore]
    public bool IsOutOfStock => QuantityInPieces <= 0;

    [Ignore]
    public string QuantityDisplay => $"{QuantityInPieces} pcs";
}
