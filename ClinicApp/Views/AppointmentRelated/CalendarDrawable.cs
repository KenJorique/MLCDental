using Microsoft.Maui.Graphics;
using ClinicApp.ViewModels;
using ClinicApp.Models.AppointmentModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public class CalendarDrawable : IDrawable
    {
        public List<CalendarDayColumn> Columns { get; set; } = new();

        // Fixed sidebar/header widths — everything else scales to the actual canvas size.
        private const float TimeColW = 40f;
        private const float HeaderH = 54f;

        // Clinic hours: 10 AM – 5 PM (17 = 5 PM slot start)
        private readonly int[] _hours = { 10, 11, 12, 13, 14, 15, 16 };

        // Recomputed every Draw() call from the real canvas size, so rows/columns fill whatever space the GraphicsView actually has.
        private float _dayColW;
        private float _rowH;
        private float _canvasWidth;

        // Gold/beige appointment block colors matching the list view cards
        private static readonly Color AppointmentFill = Color.FromArgb("#F5F0D0");
        private static readonly Color AppointmentBorder = Color.FromArgb("#C8A84B");
        private static readonly Color AppointmentText = Color.FromArgb("#1A1A2E");

        private readonly List<(RectF rect, AppointmentEntry entry)> _tapRegions = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            _tapRegions.Clear();
            canvas.Antialias = true;

            // White background
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Columns == null || Columns.Count == 0)
            {
                canvas.FontSize = 14f;
                canvas.FontColor = Colors.Gray;
                canvas.DrawString("No appointments this week",
                    dirtyRect.Width / 2, dirtyRect.Height / 2,
                    dirtyRect.Width, 40,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                return;
            }

            // Fills the whole GraphicsView: columns split the leftover width, rows split the leftover height.
            int dayCount = Math.Min(Columns.Count, 7);
            _canvasWidth = dirtyRect.Width;
            _dayColW = dayCount > 0 ? (dirtyRect.Width - TimeColW) / dayCount : dirtyRect.Width - TimeColW;
            _rowH = _hours.Length > 0 ? (dirtyRect.Height - HeaderH) / _hours.Length : dirtyRect.Height - HeaderH;

            DrawDayHeaders(canvas);
            DrawTimeGrid(canvas);
            DrawEvents(canvas);
        }

        private void DrawDayHeaders(ICanvas canvas)
        {
            for (int d = 0; d < Columns.Count && d < 7; d++)
            {
                var col = Columns[d];
                float x = TimeColW + d * _dayColW;
                float cx = x + _dayColW / 2f;

                // Day label (MON, TUE…)
                canvas.FontSize = 10f;
                canvas.FontColor = Colors.Gray;
                canvas.DrawString(col.DayLabel, x, 6, _dayColW, 18,
                    HorizontalAlignment.Center, VerticalAlignment.Center);

                // Date number — green circle if today, plain otherwise
                if (col.IsToday)
                {
                    canvas.FillColor = Color.FromArgb("#2E7D32");
                    // Circle centred on the day column
                    canvas.FillCircle(cx, 38f, 14f);
                    canvas.FontColor = Colors.White;
                }
                else
                {
                    canvas.FontColor = Color.FromArgb("#1A1A2E");
                }

                canvas.FontSize = 13f;
                canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                // DrawString: x, y, width, height — centred within the column
                canvas.DrawString(col.DayNum, x, 30f, _dayColW, 20f,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawTimeGrid(ICanvas canvas)
        {
            canvas.FontSize = 10f;
            canvas.FontColor = Colors.Gray;
            // Light green-tinted grid lines
            canvas.StrokeColor = Color.FromArgb("#DCEEE0");
            canvas.StrokeSize = 1f;

            for (int i = 0; i < _hours.Length; i++)
            {
                float y = HeaderH + i * _rowH;

                // 12 = noon label, otherwise AM/PM
                string label;
                if (_hours[i] == 12) label = "12 PM";
                else if (_hours[i] > 12) label = $"{_hours[i] - 12} PM";
                else label = $"{_hours[i]} AM";

                canvas.DrawString(label, 4, y + 6, TimeColW - 8, _rowH,
                    HorizontalAlignment.Right, VerticalAlignment.Top);
                canvas.DrawLine(TimeColW, y, _canvasWidth, y);
            }
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        }

        private void DrawEvents(ICanvas canvas)
        {
            if (Columns == null) return;

            for (int d = 0; d < Columns.Count && d < 7; d++)
            {
                var col = Columns[d];
                if (col.Slots == null) continue;

                float colX = TimeColW + d * _dayColW;

                for (int i = 0; i < col.Slots.Count && i < _hours.Length; i++)
                {
                    var slot = col.Slots[i];
                    float y = HeaderH + i * _rowH;

                    if (slot.Entry == null) continue;

                    var rect = new RectF(colX + 2, y + 4, _dayColW - 4, _rowH - 8);

                    // Gold/beige fill matching list view appointment cards
                    canvas.FillColor = AppointmentFill;
                    canvas.FillRoundedRectangle(rect, 6);

                    // Gold border
                    canvas.StrokeColor = AppointmentBorder;
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawRoundedRectangle(rect, 6);

                    // Patient name only — centered vertically in the block
                    // Wrap at ~10 chars per line to fit the day column
                    var rawName = slot.Entry.PatientName ?? "";
                    var nameParts = rawName.Split(' ');
                    // Show first name on line 1, last name initial on line 2
                    string line1 = nameParts.Length > 0 ? nameParts[0] : rawName;
                    string line2 = nameParts.Length > 1
                        ? string.Join(" ", nameParts.Skip(1)) : "";

                    // Truncate if still too long for the column — threshold scales with the now-dynamic column width.
                    int maxChars = (int)(_dayColW / 7f);
                    if (line1.Length > maxChars) line1 = line1.Substring(0, Math.Max(1, maxChars - 1)) + ".";
                    if (line2.Length > maxChars) line2 = line2.Substring(0, Math.Max(1, maxChars - 1)) + ".";

                    // Font scales gently with the taller/wider blocks instead of staying fixed at 8pt.
                    float nameFontSize = Math.Clamp(_rowH / 6.5f, 8f, 11f);
                    canvas.FontSize = nameFontSize;
                    canvas.FontColor = AppointmentText;

                    float lineH = nameFontSize + 4f;
                    float textY = string.IsNullOrEmpty(line2)
                        ? rect.Y + (rect.Height / 2) - (lineH / 2)
                        : rect.Y + (rect.Height / 2) - lineH;

                    canvas.DrawString(line1,
                        rect.X + 2, textY,
                        rect.Width - 4, lineH,
                        HorizontalAlignment.Center, VerticalAlignment.Top);

                    if (!string.IsNullOrEmpty(line2))
                        canvas.DrawString(line2,
                            rect.X + 2, textY + lineH,
                            rect.Width - 4, lineH,
                            HorizontalAlignment.Center, VerticalAlignment.Top);

                    _tapRegions.Add((rect, slot.Entry));
                }
            }
        }

        public AppointmentEntry? HitTest(float x, float y)
        {
            foreach (var (rect, entry) in _tapRegions)
                if (rect.Contains(x, y)) return entry;
            return null;
        }
    }
}
