using ClinicApp.ViewModels.CephalometricVM;

namespace ClinicApp.Views.CephalometricRelated;

public partial class CephalometricPage : ContentPage
{
    private GraphicsView? _landmarkCanvas;

    public CephalometricPage(CephalometricViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Get reference to canvas for drawing landmarks
        _landmarkCanvas = this.FindByName<GraphicsView>("LandmarkCanvas");
        if (_landmarkCanvas != null)
        {
            _landmarkCanvas.Drawable = new LandmarkDrawable(vm);
        }

        // Redraw when landmarks change
        if (BindingContext is CephalometricViewModel viewModel)
        {
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CephalometricViewModel.DetectedLandmarks))
                {
                    _landmarkCanvas?.Invalidate();
                }
            };
        }
    }
}

/// <summary>
/// Custom drawable for rendering landmark overlays on the X-ray
/// </summary>
internal class LandmarkDrawable : IDrawable
{
    private readonly CephalometricViewModel _viewModel;

    public LandmarkDrawable(CephalometricViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_viewModel?.DetectedLandmarks == null || _viewModel.DetectedLandmarks.Count == 0)
            return;

        // Get the image dimensions to scale landmarks correctly
        // Assuming the GraphicsView is the same size as the Image above it
        float canvasWidth = dirtyRect.Width;
        float canvasHeight = dirtyRect.Height;

        // Color palette for landmarks (rotate through colors for variety)
        Color[] colors = new[]
        {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow,
            Colors.Cyan, Colors.Magenta, Colors.Orange, Colors.Purple
        };

        int landmarkIndex = 0;
        foreach (var landmark in _viewModel.DetectedLandmarks)
        {
            // Draw circle at landmark position
            float radius = 8f;
            Color color = colors[landmarkIndex % colors.Length];

            // Draw filled circle
            canvas.FillColor = color;
            canvas.Alpha = 0.3f;
            canvas.FillCircle(landmark.X, landmark.Y, radius + 2);

            // Draw border circle
            canvas.StrokeColor = color;
            canvas.StrokeSize = 2;
            canvas.Alpha = 1.0f;
            canvas.DrawCircle(landmark.X, landmark.Y, radius);

            // Draw landmark label
            canvas.FontColor = color;
            canvas.FontSize = 11;
            canvas.DrawString($"{landmark.ClassId + 1}",
                            landmark.X + radius + 4, landmark.Y - radius,
                            HorizontalAlignment.Left);

            landmarkIndex++;
        }
    }
}