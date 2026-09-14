using ClinicApp.Services;
using ClinicApp.ViewModels.CephalometricVM;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace ClinicApp.Views.CephalometricRelated;

public partial class CephalometricPage : ContentPage
{
    private GraphicsView? _landmarkCanvas;
    private Landmark? _draggingLandmark;
    private const float HitRadius = 20f;
    private const float TapMoveThreshold = 6f; // px of movement before it counts as a drag, not a tap

    private LandmarkDrawable? _drawable;
    private double _canvasWidth;
    private double _canvasHeight;

    private PointF _touchStartPoint;
    private Landmark? _touchStartLandmark;
    private bool _hasMoved;

    private double _currentScale = 1;
    private double _startScale = 1;
    private const double MinScale = 1;
    private const double MaxScale = 4;

    public CephalometricPage(CephalometricViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _landmarkCanvas = this.FindByName<GraphicsView>("LandmarkCanvas");
        if (_landmarkCanvas != null)
        {
            _drawable = new LandmarkDrawable(vm);
            _landmarkCanvas.Drawable = _drawable;
            _landmarkCanvas.SizeChanged += OnCanvasSizeChanged;
        }


        if (BindingContext is CephalometricViewModel viewModel)
        {
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CephalometricViewModel.DetectedLandmarks) ||
                    e.PropertyName == nameof(CephalometricViewModel.SoftTissueOutline) ||
                    e.PropertyName == nameof(CephalometricViewModel.IncompletePlanes))
                {
                    if (_drawable != null)
                        _drawable.IncompletePlanes = viewModel.IncompletePlanes.ToHashSet();

                    UpdateScaling();
                    _landmarkCanvas?.Invalidate();
                }
                else if (e.PropertyName == nameof(CephalometricViewModel.ImagePath))
                {
                    UpdateScaling();
                    _landmarkCanvas?.Invalidate();
                }
            };
        }
    }

    private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        var content = this.FindByName<Grid>("ImageContentGrid");
        if (content == null) return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                _startScale = content.Scale;
                content.AnchorX = 0;
                content.AnchorY = 0;
                break;

            case GestureStatus.Running:
                _currentScale = Math.Clamp(_startScale * e.Scale, MinScale, MaxScale);

                // Adjust anchor so zoom centers roughly on the pinch point
                double renderedX = content.X + e.ScaleOrigin.X * content.Width * content.Scale;
                double renderedY = content.Y + e.ScaleOrigin.Y * content.Height * content.Scale;

                content.AnchorX = e.ScaleOrigin.X;
                content.AnchorY = e.ScaleOrigin.Y;
                content.Scale = _currentScale;
                break;

            case GestureStatus.Completed:
                _startScale = content.Scale;
                break;
        }
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (_landmarkCanvas == null) return;
        _canvasWidth = _landmarkCanvas.Width;
        _canvasHeight = _landmarkCanvas.Height;
        UpdateScaling();
        _landmarkCanvas.Invalidate();
    }

    private void UpdateScaling()
    {
        if (_drawable == null || BindingContext is not CephalometricViewModel vm) return;
        if (string.IsNullOrEmpty(vm.ImagePath) || !File.Exists(vm.ImagePath)) return;
        if (_canvasWidth <= 0 || _canvasHeight <= 0) return;

        try
        {
            var info = ImageSharpImage.Identify(vm.ImagePath);
            if (info == null) return;

            _drawable.SetTransform(info.Width, info.Height, _canvasWidth, _canvasHeight);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? Could not read image dimensions: {ex.Message}");
        }
    }

    private void OnCanvasStartInteraction(object sender, TouchEventArgs e)
    {
        if (BindingContext is not CephalometricViewModel vm || _drawable == null) return;
        var touch = e.Touches?.FirstOrDefault() ?? default;

        // Placement mode: next tap creates the missing landmark here, skip hit-testing
        if (!string.IsNullOrEmpty(vm.LandmarkBeingPlaced))
        {
            var (origX, origY) = _drawable.ToOriginal(touch.X, touch.Y);

            // With this corrected line (remove the trailing comma and supply required arguments):
            // According to the signature: PlaceLandmarkAt(string className, float x, float y, int imageWidth, int imageHeight);
            // You need to provide imageWidth and imageHeight. Use _drawable._originalWidth and _drawable._originalHeight if accessible, or get them from the image info if needed.

            vm.PlaceLandmarkAt(vm.LandmarkBeingPlaced, origX, origY, (int)_drawable._originalWidth, (int)_drawable._originalHeight);
            _landmarkCanvas?.Invalidate();
            return;
        }


        _touchStartPoint = touch;
        _hasMoved = false;

        var hit = vm.DetectedLandmarks
            .Select(l => (Landmark: l, Display: _drawable.ToDisplay(l.X, l.Y)))
            .Where(t => Distance(t.Display.x, t.Display.y, touch.X, touch.Y) <= HitRadius)
            .OrderBy(t => Distance(t.Display.x, t.Display.y, touch.X, touch.Y))
            .Select(t => t.Landmark)
            .FirstOrDefault();

        _draggingLandmark = hit;
        _touchStartLandmark = hit;
    }

    private void OnCanvasDragInteraction(object sender, TouchEventArgs e)
    {
        if (_draggingLandmark == null || _drawable == null) return;
        var touch = e.Touches?.FirstOrDefault() ?? default;

        if (Distance(_touchStartPoint.X, _touchStartPoint.Y, touch.X, touch.Y) > TapMoveThreshold)
            _hasMoved = true;

        var (origX, origY) = _drawable.ToOriginal(touch.X, touch.Y);
        _draggingLandmark.X = origX;
        _draggingLandmark.Y = origY;
        _landmarkCanvas?.Invalidate();
    }

    private void OnCanvasEndInteraction(object sender, TouchEventArgs e)
    {
        // A tap (touched a dot, barely moved) toggles its label on the image
        // instead of correcting its position.
        if (_touchStartLandmark != null && !_hasMoved && _drawable != null)
        {
            _drawable.SelectedLandmark = _drawable.SelectedLandmark == _touchStartLandmark
                ? null
                : _touchStartLandmark;
            _landmarkCanvas?.Invalidate();
        }

        _draggingLandmark = null;
        _touchStartLandmark = null;
        _hasMoved = false;
    }

    private static double Distance(float x1, float y1, float x2, float y2) =>
        Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));

    private async void OnPlacementModeChanged(string? landmarkName)
    {
        if (_drawable == null) return;
        _drawable.PlacementTargetName = landmarkName;
        _landmarkCanvas?.Invalidate();

        if (string.IsNullOrEmpty(landmarkName)) return;

        var region = LandmarkRegionGuide.GetRegion(landmarkName);
        if (region == null) return;

        var scrollView = this.FindByName<ScrollView>("ImageScrollView");
        var content = this.FindByName<Grid>("ImageContentGrid");
        if (scrollView == null || content == null) return;

        double targetScale = 2.5;
        content.AnchorX = 0;
        content.AnchorY = 0;
        content.Scale = targetScale;

        // Wait a frame for layout to catch up to the new scale before scrolling
        await Task.Delay(50);

        double scrollX = (region.CenterX * content.Width * targetScale) - (scrollView.Width / 2);
        double scrollY = (region.CenterY * content.Height * targetScale) - (scrollView.Height / 2);

        await scrollView.ScrollToAsync(Math.Max(0, scrollX), Math.Max(0, scrollY), animated: true);
    }
}

internal class LandmarkDrawable : IDrawable
{
    private readonly CephalometricViewModel _viewModel;

    public double _originalWidth = 1;
    public double _originalHeight = 1;
    private double _canvasWidth = 1;
    private double _canvasHeight = 1;
    private double _scale = 1;
    private double _offsetX = 0;
    private double _offsetY = 0;
    public string? PlacementTargetName { get; set; }

    /// <summary>Landmark tapped by the user; its full name is drawn on the image until tapped again.</summary>
    public Landmark? SelectedLandmark { get; set; }
    public HashSet<string> IncompletePlanes { get; set; } = new();

    private static readonly HashSet<string> LowConfidenceClasses = new()
    {
        "Gonion", "Orbitale", "Porion", "Subspinale", "Supramentale", "Articulare", "Soft tissue pogonion"
    };

    public LandmarkDrawable(CephalometricViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void SetTransform(int originalWidth, int originalHeight, double canvasWidth, double canvasHeight)
    {
        _originalWidth = Math.Max(originalWidth, 1);
        _originalHeight = Math.Max(originalHeight, 1);
        _canvasWidth = Math.Max(canvasWidth, 1);
        _canvasHeight = Math.Max(canvasHeight, 1);

        double scaleX = _canvasWidth / _originalWidth;
        double scaleY = _canvasHeight / _originalHeight;
        _scale = Math.Min(scaleX, scaleY);

        _offsetX = (_canvasWidth - _originalWidth * _scale) / 2.0;
        _offsetY = (_canvasHeight - _originalHeight * _scale) / 2.0;
    }

    public (float x, float y) ToDisplay(float originalX, float originalY)
    {
        float x = (float)(_offsetX + originalX * _scale);
        float y = (float)(_offsetY + originalY * _scale);
        return (x, y);
    }

    public (float x, float y) ToOriginal(float displayX, float displayY)
    {
        if (_scale <= 0) return (displayX, displayY);
        float x = (float)((displayX - _offsetX) / _scale);
        float y = (float)((displayY - _offsetY) / _scale);
        return (x, y);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_viewModel?.DetectedLandmarks == null || _viewModel.DetectedLandmarks.Count == 0)
            return;
    

        var landmarks = _viewModel.DetectedLandmarks;

        DrawOutline(canvas, _viewModel.SoftTissueOutline);

        Landmark? Find(string name) =>
            landmarks.FirstOrDefault(l => string.Equals(l.ClassName, name, StringComparison.OrdinalIgnoreCase));

        DrawPlaneLine(canvas, "S-N line", Find("Sella"), Find("Nasion"), Colors.Gold, extendPastSecond: 40);
        DrawPlaneLine(canvas, "N-A line", Find("Nasion"), Find("Subspinale"), Colors.Cyan);
        DrawPlaneLine(canvas, "N-B line", Find("Nasion"), Find("Supramentale"), Colors.Magenta);
        DrawPlaneLine(canvas, "Frankfort plane", Find("Porion"), Find("Orbitale"), Colors.Orange, extendBothWays: 30);
        DrawPlaneLine(canvas, "Mandibular plane", Find("Gonion"), Find("Menton"), Colors.LimeGreen, extendBothWays: 30);

        if (!string.IsNullOrEmpty(PlacementTargetName))
        {
            DrawPlacementGuide(canvas, PlacementTargetName);
        }

        for (int i = 0; i < landmarks.Count; i++)
        {
            var landmark = landmarks[i];
            var (dx, dy) = ToDisplay(landmark.X, landmark.Y);

            float radius = 9f;
            Color color = LandmarkColors.GetColor(landmark.ClassId);
            bool needsReview = LowConfidenceClasses.Contains(landmark.ClassName ?? "");
            bool isSelected = ReferenceEquals(landmark, SelectedLandmark);

            bool isManuallyPlaced = landmark.Confidence == 0f;

            canvas.StrokeColor = isSelected ? Colors.Black
                : isManuallyPlaced ? Colors.DodgerBlue
                : (needsReview ? Colors.DarkOrange : Colors.White);
            canvas.StrokeSize = isSelected ? 3f : isManuallyPlaced ? 3f : (needsReview ? 2.5f : 1.5f);

            canvas.FillColor = color;
            canvas.Alpha = 0.25f;
            canvas.FillCircle(dx, dy, radius + 3);

            canvas.FillColor = color;
            canvas.Alpha = 1.0f;
            canvas.FillCircle(dx, dy, radius);

            canvas.StrokeColor = isSelected ? Colors.Black : (needsReview ? Colors.DarkOrange : Colors.White);
            canvas.StrokeSize = isSelected ? 3f : (needsReview ? 2.5f : 1.5f);
            canvas.StrokeDashPattern = !isSelected && needsReview ? new float[] { 3, 2 } : null;
            canvas.DrawCircle(dx, dy, radius);

            string number = landmark.Index > 0 ? landmark.Index.ToString() : (i + 1).ToString();
            canvas.FontColor = Colors.White;
            canvas.FontSize = 10;
            var numSize = canvas.GetStringSize(number, Microsoft.Maui.Graphics.Font.DefaultBold, 10);
            canvas.DrawString(number, dx - numSize.Width / 2, dy - numSize.Height / 2, HorizontalAlignment.Left);
        }

        // Draw the selected landmark's name last so it's always on top of everything else
        if (SelectedLandmark != null && landmarks.Contains(SelectedLandmark))
        {
            DrawSelectedLabel(canvas, SelectedLandmark);
        }

    }

    private void DrawSelectedLabel(ICanvas canvas, Landmark landmark)
    {
        var (dx, dy) = ToDisplay(landmark.X, landmark.Y);
        string label = landmark.ClassName ?? "?";

        canvas.FontSize = 12;
        var textSize = canvas.GetStringSize(label, Microsoft.Maui.Graphics.Font.DefaultBold, 12);

        float boxW = textSize.Width + 12;
        float boxH = textSize.Height + 8;

        // Default: box sits above-right of the dot
        float boxX = dx + 14;
        float boxY = dy - 14 - boxH;

        // Clamp so the label never runs off any edge of the canvas
        boxX = (float)Math.Clamp(boxX, 4, _canvasWidth - boxW - 4);
        boxY = (float)Math.Clamp(boxY, 4, _canvasHeight - boxH - 4);

        Color color = LandmarkColors.GetColor(landmark.ClassId);

        canvas.FillColor = Colors.Black;
        canvas.Alpha = 0.75f;
        canvas.FillRoundedRectangle(boxX, boxY, boxW, boxH, 5);

        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.5f;
        canvas.Alpha = 1.0f;
        canvas.DrawRoundedRectangle(boxX, boxY, boxW, boxH, 5);

        canvas.FontColor = Colors.White;
        canvas.FontSize = 12;
        canvas.DrawString(label, boxX + 6, boxY + 4, HorizontalAlignment.Left);
    }

    private void DrawOutline(ICanvas canvas, List<OutlinePoint>? outline)
    {
        if (outline == null || outline.Count < 2) return;

        var path = new PathF();
        var first = ToDisplay(outline[0].X, outline[0].Y);
        path.MoveTo(first.x, first.y);

        for (int i = 1; i < outline.Count; i++)
        {
            var pt = ToDisplay(outline[i].X, outline[i].Y);
            path.LineTo(pt.x, pt.y);
        }
        // no path.Close() — this is an open profile curve now, not a closed silhouette

        canvas.StrokeColor = Colors.LightSkyBlue;
        canvas.StrokeSize = 2f;
        canvas.Alpha = 0.9f;
        canvas.DrawPath(path);
        canvas.Alpha = 1.0f;
    }

    private void DrawPlaneLine(ICanvas canvas, string planeName, Landmark? a, Landmark? b, Color color,
      float extendPastSecond = 0, float extendBothWays = 0)
    {
        // If either endpoint is missing, there's nothing to draw — this is the
        // expected, already-correct behavior (see IncompletePlanes for the
        // matching text warning shown elsewhere in the UI).
        if (a == null || b == null) return;

        var (x1, y1) = ToDisplay(a.X, a.Y);
        var (x2, y2) = ToDisplay(b.X, b.Y);

        float ext = extendBothWays > 0 ? extendBothWays : extendPastSecond;
        if (ext > 0)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0)
            {
                float ux = dx / len, uy = dy / len;
                x2 += ux * ext;
                y2 += uy * ext;
                if (extendBothWays > 0)
                {
                    x1 -= ux * extendBothWays;
                    y1 -= uy * extendBothWays;
                }
            }
        }

        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.5f;
        canvas.Alpha = 0.85f;
        canvas.DrawLine(x1, y1, x2, y2);
        canvas.Alpha = 1.0f;
    }

    private void DrawPlacementGuide(ICanvas canvas, string landmarkName)
    {
        var region = LandmarkRegionGuide.GetRegion(landmarkName);
        if (region == null) return;

        // Region is normalized to the ORIGINAL image, so convert through ToDisplay
        float origX = region.CenterX * (float)_originalWidth;
        float origY = region.CenterY * (float)_originalHeight;
        var (dx, dy) = ToDisplay(origX, origY);

        float radius = region.RadiusFraction * (float)Math.Min(_originalWidth, _originalHeight) * (float)_scale;

        canvas.StrokeColor = Colors.DodgerBlue;
        canvas.StrokeSize = 2f;
        canvas.StrokeDashPattern = new float[] { 6, 4 };
        canvas.Alpha = 0.8f;
        canvas.DrawCircle(dx, dy, radius);

        canvas.FillColor = Colors.DodgerBlue;
        canvas.Alpha = 0.08f;
        canvas.FillCircle(dx, dy, radius);
        canvas.Alpha = 1.0f;
    }

}