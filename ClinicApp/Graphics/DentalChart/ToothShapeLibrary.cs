// File: Graphics/DentalChart/ToothShapeLibrary.cs
using SkiaSharp;

namespace ClinicApp.Graphics.DentalChart;

public enum ToothShapeCategory
{
    CentralIncisor,
    LateralIncisor,
    Canine,
    Premolar,
    Molar
}

public static class ToothShapeLibrary
{
    /// <summary>
    /// Maps a Universal Numbering System tooth (1-32) to a shape category,
    /// matching the exact ordering used in ToothViewModel.ToothName.
    /// </summary>
    public static ToothShapeCategory GetCategory(int universalToothNumber)
    {
        int posInQuadrant = ((universalToothNumber - 1) % 8) + 1; // 1..8

        // Quadrants 1-8 and 17-24 run 3rdMolar -> Central (pos1..pos8)
        // Quadrants 9-16 and 25-32 run Central -> 3rdMolar (pos1..pos8, reversed)
        bool reversed = (universalToothNumber is >= 9 and <= 16)
                      || (universalToothNumber is >= 25 and <= 32);

        int canonicalPos = reversed ? (9 - posInQuadrant) : posInQuadrant;
        // canonicalPos: 1=3rdMolar, 2=2ndMolar, 3=1stMolar, 4=2ndPremolar,
        //               5=1stPremolar, 6=Canine, 7=LateralIncisor, 8=CentralIncisor

        return canonicalPos switch
        {
            1 or 2 or 3 => ToothShapeCategory.Molar,
            4 or 5 => ToothShapeCategory.Premolar,
            6 => ToothShapeCategory.Canine,
            7 => ToothShapeCategory.LateralIncisor,
            _ => ToothShapeCategory.CentralIncisor
        };
    }

    // All shapes are built in local space with the pivot at (0,0) = the
    // gum-line base of the tooth. The crown extends upward (negative Y).
    // This lets DentalArchCanvasView translate to the pivot point on the
    // arch and rotate around it without extra offset math.

    public static SKPath GetOutline(ToothShapeCategory category) => category switch
    {
        ToothShapeCategory.CentralIncisor => BuildIncisor(halfWidth: 10, height: 34),
        ToothShapeCategory.LateralIncisor => BuildIncisor(halfWidth: 8, height: 29),
        ToothShapeCategory.Canine => BuildCanine(),
        ToothShapeCategory.Premolar => BuildPremolar(),
        ToothShapeCategory.Molar => BuildMolar(),
        _ => BuildIncisor(10, 34)
    };

    public static SKPath GetCrackLines(ToothShapeCategory category) => category switch
    {
        ToothShapeCategory.Canine => CanineCracks(),
        ToothShapeCategory.Premolar => PremolarCracks(),
        ToothShapeCategory.Molar => MolarCracks(),
        _ => new SKPath() // incisors stay smooth, no cusp texture
    };

    private static SKPath BuildIncisor(float halfWidth, float height)
    {
        var p = new SKPath();
        float hw = halfWidth, h = height;
        p.MoveTo(-hw + 1, 0);
        p.CubicTo(-hw, -h * 0.3f, -hw, -h * 0.7f, -hw * 0.6f, -h);
        p.CubicTo(-hw * 0.3f, -h - 3, hw * 0.3f, -h - 3, hw * 0.6f, -h);
        p.CubicTo(hw, -h * 0.7f, hw, -h * 0.3f, hw - 1, 0);
        p.CubicTo(hw * 0.5f, -3, -hw * 0.5f, -3, -hw + 1, 0);
        p.Close();
        return p;
    }

    private static SKPath BuildCanine()
    {
        var p = new SKPath();
        p.MoveTo(-9, 0);
        p.CubicTo(-10, -8, -9, -16, -6, -24);
        p.LineTo(0, -34);
        p.LineTo(6, -24);
        p.CubicTo(9, -16, 10, -8, 9, 0);
        p.CubicTo(5, -3, -5, -3, -9, 0);
        p.Close();
        return p;
    }

    private static SKPath CanineCracks()
    {
        var p = new SKPath();
        p.MoveTo(0, -32);
        p.LineTo(0, -4);
        return p;
    }

    private static SKPath BuildPremolar()
    {
        var p = new SKPath();
        p.MoveTo(-11, 0);
        p.CubicTo(-13, -10, -12, -22, -8, -28);
        p.CubicTo(-6, -31, -2, -29, 0, -27);
        p.CubicTo(2, -29, 6, -31, 8, -28);
        p.CubicTo(12, -22, 13, -10, 11, 0);
        p.CubicTo(6, -3, -6, -3, -11, 0);
        p.Close();
        return p;
    }

    private static SKPath PremolarCracks()
    {
        var p = new SKPath();
        p.MoveTo(0, -27);
        p.LineTo(0, -2);
        p.MoveTo(-6, -13);
        p.LineTo(6, -13);
        return p;
    }

    private static SKPath BuildMolar()
    {
        var p = new SKPath();
        p.MoveTo(-13, -6);
        p.CubicTo(-13, -16, -13, -24, -8, -28);
        p.CubicTo(-4, -31, 4, -31, 8, -28);
        p.CubicTo(13, -24, 13, -16, 13, -6);
        p.CubicTo(13, -2, 10, 0, 6, 0);
        p.LineTo(-6, 0);
        p.CubicTo(-10, 0, -13, -2, -13, -6);
        p.Close();
        return p;
    }

    private static SKPath MolarCracks()
    {
        var p = new SKPath();
        p.MoveTo(0, -29);
        p.LineTo(0, -13);
        p.MoveTo(-9, -19);
        p.LineTo(8, -10);
        p.MoveTo(-9, -10);
        p.LineTo(8, -19);
        return p;
    }
}