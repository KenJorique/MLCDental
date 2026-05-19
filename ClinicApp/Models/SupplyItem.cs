using SQLite;

namespace ClinicApp.Models;

[Table("SupplyItem")]
public class SupplyItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. "Box", "Bottle", "Pack", "Piece"</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>How many individual pieces are in one Unit (e.g. 50 gloves per box). 1 if unit is already a piece.</summary>
    public int PiecesPerUnit { get; set; } = 1;

    /// <summary>Total quantity in pieces currently in stock.</summary>
    public int QuantityInPieces { get; set; } = 0;

    /// <summary>Optional: size or variant label (e.g. "Small", "Large", "250mg")</summary>
    public string SizeVariant { get; set; } = string.Empty;

    /// <summary>Whether the item has an expiration date to track.</summary>
    public bool HasExpiration { get; set; } = false;

    /// <summary>ISO date string for expiration. Empty if not applicable.</summary>
    public string ExpirationDate { get; set; } = string.Empty;

    /// <summary>Quantity in pieces at or below which a low-stock alert is shown.</summary>
    public int MinimumStockPieces { get; set; } = 10;

    public string AddedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    // ── Computed (not stored) ──────────────────────────────────────
    [Ignore]
    public bool IsLowStock => QuantityInPieces <= MinimumStockPieces;

    [Ignore]
    public bool IsOutOfStock => QuantityInPieces <= 0;

    [Ignore]
    public string QuantityDisplay =>
        PiecesPerUnit > 1
            ? $"{QuantityInPieces} pcs  ({QuantityInPieces / PiecesPerUnit} {Unit}{(QuantityInPieces / PiecesPerUnit == 1 ? "" : "s")} + {QuantityInPieces % PiecesPerUnit} pcs)"
            : $"{QuantityInPieces} {Unit}{(QuantityInPieces == 1 ? "" : "s")}";
}
