using Microsoft.Maui.Graphics;
using ClinicApp.Models;
using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public class CalendarDrawable : IDrawable
    {
        public List<CalendarDayColumn> Columns { get; set; } = new();

        private const float TimeColW = 50f;
        private const float DayColW = 46f;
        private const float RowH = 48f;
        private const float HeaderH = 50f;
        private readonly int[] _hours = { 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        private readonly List<(RectF rect, AppointmentEntry entry)> _tapRegions = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            _tapRegions.Clear();
            canvas.Antialias = true;

            canvas.FillColor = Color.FromArgb("#F5F5F5");
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

            DrawDayHeaders(canvas);
            DrawTimeGrid(canvas);
            DrawEvents(canvas);
        }

        private void DrawDayHeaders(ICanvas canvas)
        {
            for (int d = 0; d < Columns.Count && d < 7; d++)
            {
                var col = Columns[d];
                float x = TimeColW + d * DayColW;
                float cx = x + DayColW / 2;

                canvas.FontSize = 9f;
                canvas.FontColor = Colors.Gray;
                canvas.DrawString(col.DayLabel, x, 8, DayColW, 20, HorizontalAlignment.Center, VerticalAlignment.Center);

                if (col.IsToday)
                {
                    canvas.FillColor = Color.FromArgb("#4A4A8A");
                    canvas.FillCircle(cx, 35, 14);
                    canvas.FontColor = Colors.White;
                }
                else
                {
                    canvas.FontColor = Colors.Black;
                }

                canvas.FontSize = 13f;
                canvas.DrawString(col.DayNum, x, 28, DayColW, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        private void DrawTimeGrid(ICanvas canvas)
        {
            canvas.FontSize = 10f;
            canvas.FontColor = Colors.Gray;
            canvas.StrokeColor = Color.FromArgb("#E0E0E0");
            canvas.StrokeSize = 1f;

            for (int i = 0; i < _hours.Length; i++)
            {
                float y = HeaderH + i * RowH;
                string label = _hours[i] >= 12 ? $"{_hours[i] - 12} PM" : $"{_hours[i]} AM";

                canvas.DrawString(label, 4, y + 8, TimeColW - 8, RowH, HorizontalAlignment.Right, VerticalAlignment.Top);
                canvas.DrawLine(TimeColW, y, 500, y);
            }
        }

        private void DrawEvents(ICanvas canvas)
        {
            if (Columns == null) return;

            for (int d = 0; d < Columns.Count && d < 7; d++)
            {
                var col = Columns[d];
                if (col.Slots == null) continue;

                float colX = TimeColW + d * DayColW;

                for (int i = 0; i < col.Slots.Count && i < _hours.Length; i++)
                {
                    var slot = col.Slots[i];
                    float y = HeaderH + i * RowH;

                   

                    if (slot.Entry == null) continue;

                    var rect = new RectF(colX + 3, y + 4, DayColW - 6, RowH - 8);
                    canvas.FillColor = slot.Entry.StatusColor;
                    canvas.FillRoundedRectangle(rect, 6);

                    // Event text
                    canvas.FontSize = 8f;
                    canvas.FontColor = Colors.White;
                    canvas.DrawString(slot.HourLabel,
                        rect.X + 4, rect.Y + 4,
                        rect.Width - 8, 14,
                        HorizontalAlignment.Left, VerticalAlignment.Top);

                    canvas.FontSize = 7f;
                    var name = slot.Entry.PatientName.Length > 9
                        ? slot.Entry.PatientName.Substring(0, 9) + ".."
                        : slot.Entry.PatientName;

                    canvas.DrawString(name,
                        rect.X + 4, rect.Y + 18,
                        rect.Width - 8, 14,
                        HorizontalAlignment.Left, VerticalAlignment.Top);

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