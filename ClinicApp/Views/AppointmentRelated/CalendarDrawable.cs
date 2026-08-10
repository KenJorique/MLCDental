using Microsoft.Maui.Graphics;
using ClinicApp.Models;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public class CalendarDrawable : IDrawable
    {
        public List<CalendarDayColumn> Columns { get; set; } = new();

        private const float TimeColW = 50f;
        private const float DayColW = 46f;
        private const float RowH = 52f;   // 7 rows × 52 + 54 header = 418px
        private const float HeaderH = 54f;

        // Clinic hours: 10 AM – 5 PM (17 = 5 PM slot start)
        private readonly int[] _hours = { 10, 11, 12, 13, 14, 15, 16 };

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
                float cx = x + DayColW / 2f;

                // Day label (MON, TUE…)
                canvas.FontSize = 9f;
                canvas.FontColor = Colors.Gray;
                canvas.DrawString(col.DayLabel, x, 6, DayColW, 18,
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
                // DrawString: x, y, width, height — centred within the column
                canvas.DrawString(col.DayNum, x, 30f, DayColW, 20f,
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
                float y = HeaderH + i * RowH;

                // 12 = noon label, otherwise AM/PM
                string label;
                if (_hours[i] == 12) label = "12 PM";
                else if (_hours[i] > 12) label = $"{_hours[i] - 12} PM";
                else label = $"{_hours[i]} AM";

                canvas.DrawString(label, 4, y + 6, TimeColW - 8, RowH,
                    HorizontalAlignment.Right, VerticalAlignment.Top);
                canvas.DrawLine(TimeColW, y, 600, y);
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

                    var rect = new RectF(colX + 2, y + 4, DayColW - 4, RowH - 8);

                    // Gold/beige fill matching list view appointment cards
                    canvas.FillColor = AppointmentFill;
                    canvas.FillRoundedRectangle(rect, 6);

                    // Gold border
                    canvas.StrokeColor = AppointmentBorder;
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawRoundedRectangle(rect, 6);

                    // Patient name only — centered vertically in the block
                    // Wrap at ~10 chars per line to fit the narrow column
                    var rawName = slot.Entry.PatientName ?? "";
                    var nameParts = rawName.Split(' ');
                    // Show first name on line 1, last name initial on line 2
                    string line1 = nameParts.Length > 0 ? nameParts[0] : rawName;
                    string line2 = nameParts.Length > 1
                        ? string.Join(" ", nameParts.Skip(1)) : "";

                    // Truncate if still too long for column
                    if (line1.Length > 7) line1 = line1.Substring(0, 6) + ".";
                    if (line2.Length > 7) line2 = line2.Substring(0, 6) + ".";

                    canvas.FontSize = 8f;
                    canvas.FontColor = AppointmentText;

                    float textY = string.IsNullOrEmpty(line2)
                        ? rect.Y + (rect.Height / 2) - 6
                        : rect.Y + (rect.Height / 2) - 12;

                    canvas.DrawString(line1,
                        rect.X + 2, textY,
                        rect.Width - 4, 14,
                        HorizontalAlignment.Center, VerticalAlignment.Top);

                    if (!string.IsNullOrEmpty(line2))
                        canvas.DrawString(line2,
                            rect.X + 2, textY + 14,
                            rect.Width - 4, 14,
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
