// File: Graphics/DentalChart/DentalArchCanvasView.cs
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using ClinicApp.ViewModels.DentalChart;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ClinicApp.Graphics.DentalChart;

public enum ArchPosition { Upper, Lower }

public class DentalArchCanvasView : SKCanvasView
{
    // ── Bindable properties ─────────────────────────────────────
    public static readonly BindableProperty TeethSourceProperty =
        BindableProperty.Create(nameof(TeethSource), typeof(IEnumerable),
            typeof(DentalArchCanvasView), null, propertyChanged: OnTeethSourceChanged);

    public static readonly BindableProperty ArchProperty =
        BindableProperty.Create(nameof(Arch), typeof(ArchPosition),
            typeof(DentalArchCanvasView), ArchPosition.Upper,
            propertyChanged: (b, o, n) => ((DentalArchCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty TappedCommandProperty =
        BindableProperty.Create(nameof(TappedCommand), typeof(ICommand),
            typeof(DentalArchCanvasView), null);

    public IEnumerable TeethSource
    {
        get => (IEnumerable)GetValue(TeethSourceProperty);
        set => SetValue(TeethSourceProperty, value);
    }

    public ArchPosition Arch
    {
        get => (ArchPosition)GetValue(ArchProperty);
        set => SetValue(ArchProperty, value);
    }

    public ICommand TappedCommand
    {
        get => (ICommand)GetValue(TappedCommandProperty);
        set => SetValue(TappedCommandProperty, value);
    }

    // Hit-test regions rebuilt every paint pass
    private readonly List<(SKPoint Center, float Radius, ToothViewModel Vm)> _hitRegions = new();

    public DentalArchCanvasView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;
        PaintSurface += OnPaintSurface;
    }

    private static void OnTeethSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (DentalArchCanvasView)bindable;

        if (oldValue is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= view.OnCollectionChanged;
        if (oldValue is IEnumerable<ToothViewModel> oldItems)
            foreach (var t in oldItems) t.PropertyChanged -= view.OnToothPropertyChanged;

        if (newValue is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += view.OnCollectionChanged;
        if (newValue is IEnumerable<ToothViewModel> newItems)
            foreach (var t in newItems) t.PropertyChanged += view.OnToothPropertyChanged;

        view.InvalidateSurface();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ToothViewModel t in e.OldItems) t.PropertyChanged -= OnToothPropertyChanged;
        if (e.NewItems != null)
            foreach (ToothViewModel t in e.NewItems) t.PropertyChanged += OnToothPropertyChanged;

        InvalidateSurface();
    }

    private void OnToothPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        InvalidateSurface();

    // ── Painting ─────────────────────────────────────────────────
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);
        _hitRegions.Clear();

        var teeth = TeethSource?.Cast<ToothViewModel>().ToList();
        if (teeth == null || teeth.Count == 0) return;

        int count = teeth.Count; // expected 16 per arch
        float cx = info.Width / 2f;

        // Arch sizing — front teeth sit near the outer edge (away from
        // gum line), molars curve down toward the gum line at the sides.
        float a = info.Width * 0.42f;        // horizontal spread
        float topPad = info.Height * 0.14f;
        float bottomPad = info.Height * 0.12f;
        float usableDepth = info.Height - topPad - bottomPad;

        float angleSpreadDeg = 210f;
        float maxThetaRad = (float)(angleSpreadDeg / 2 * Math.PI / 180.0);
        float maxDepthFactor = 1f - (float)Math.Cos(maxThetaRad); // normalizer

        float mid = (count - 1) / 2f;

        using var outlineStroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xB8, 0xC4),
            StrokeWidth = 1.4f,
            IsAntialias = true
        };
        using var crackStroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x9A, 0xA3, 0xAE),
            StrokeWidth = 1f,
            IsAntialias = true
        };
        using var selectionStroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xA5, 0x00),
            StrokeWidth = 2.5f,
            IsAntialias = true
        };
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(0x55, 0x55, 0x55),
            IsAntialias = true
        };
        using var labelFont = new SKFont
        {
            Size = 10
        };

        for (int i = 0; i < count; i++)
        {
            var tooth = teeth[i];
            float thetaDeg = (i - mid) * (angleSpreadDeg / (count - 1));
            float thetaRad = (float)(thetaDeg * Math.PI / 180.0);

            float x = cx + a * (float)Math.Sin(thetaRad);
            float depthFactor = (1f - (float)Math.Cos(thetaRad)) / maxDepthFactor; // 0 at front tooth, 1 at last molar

            float y = Arch == ArchPosition.Upper
                ? topPad + usableDepth * depthFactor                 // front teeth near top, molars near gum line
                : (info.Height - bottomPad) - usableDepth * depthFactor; // front teeth near bottom, molars near gum line

            float rotation = Arch == ArchPosition.Upper ? thetaDeg : thetaDeg + 180f;

            var category = ToothShapeLibrary.GetCategory(tooth.ToothNumber);
            var outline = ToothShapeLibrary.GetOutline(category);
            var cracks = ToothShapeLibrary.GetCrackLines(category);

            const float toothScale = 1.8f; // bump this up/down to taste

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(rotation);
            canvas.Scale(toothScale);

            if (tooth.IsSelected)
            {
                using var glowPath = new SKPath(outline);
                canvas.DrawPath(glowPath, selectionStroke);
            }

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = ToSkColor(tooth.ToothColor)
            };
            canvas.DrawPath(outline, fillPaint);
            canvas.DrawPath(outline, outlineStroke);
            if (!cracks.IsEmpty) canvas.DrawPath(cracks, crackStroke);

            canvas.Restore();

            // Tooth number label placed just outside the shape, away from gum line
            float lx = x;
            float ly = Arch == ArchPosition.Upper ? y - 16f : y + 16f;

            string label = tooth.ToothLabel;
            int glyphCount = labelFont.CountGlyphs(label.AsSpan());
            var glyphs = new ushort[glyphCount];
            labelFont.GetGlyphs(label.AsSpan(), glyphs.AsSpan());

            float textWidth = labelFont.MeasureText(glyphs);

            using var builder = new SKTextBlobBuilder();
            var runBuffer = builder.AllocateRun(labelFont, glyphCount, 0, 0);
            glyphs.CopyTo(runBuffer.GetGlyphSpan());
            using var textBlob = builder.Build();

            if (textBlob != null)
            {
                canvas.DrawText(textBlob, lx - textWidth / 2f, ly, labelPaint);
            }

            _hitRegions.Add((new SKPoint(x, y), 22f, tooth));

            outline.Dispose();
            cracks.Dispose();
        }
    }

    private static SKColor ToSkColor(Microsoft.Maui.Graphics.Color c) =>
        new((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255), (byte)(c.Alpha * 255));

    // ── Touch / tap-to-select ───────────────────────────────────
    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType != SKTouchAction.Pressed) return;

        var point = e.Location; // already in canvas pixel space
        (SKPoint Center, float Radius, ToothViewModel Vm)? closest = null;
        float closestDist = float.MaxValue;

        foreach (var region in _hitRegions)
        {
            float dist = SKPoint.Distance(region.Center, point);
            if (dist <= region.Radius && dist < closestDist)
            {
                closestDist = dist;
                closest = region;
            }
        }

        if (closest is { } hit && TappedCommand?.CanExecute(hit.Vm.ToothNumber) == true)
            TappedCommand.Execute(hit.Vm.ToothNumber);

        e.Handled = true;
    }
}