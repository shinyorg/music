using Microsoft.Maui.Graphics;

namespace MusicSample;

static class ThemePalette
{
    static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    public static Color Accent => IsDark ? Color.FromArgb("#A29BFE") : Color.FromArgb("#6C5CE7");
    public static Color Dim => IsDark ? Color.FromArgb("#3A3A4E") : Color.FromArgb("#D8D4EC");
    public static Color Cursor => IsDark ? Colors.White : Color.FromArgb("#2D1B69");

    // VU meter ramp: calm → hot.
    public static readonly Color VuLow = Color.FromArgb("#28C2D1");
    public static readonly Color VuMid = Color.FromArgb("#F7B548");
    public static readonly Color VuHigh = Color.FromArgb("#FD79A8");
}

/// <summary>
/// Draws the track envelope as mirrored bars. Bars up to the play-head are drawn in the accent colour
/// (the "played" region, like a filled progress bar); the rest are dimmed. Falls back to a plain filled
/// progress bar when no waveform is available (DRM tracks). A cursor marks the current position.
/// </summary>
sealed class WaveformDrawable(WaveformViewModel vm) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        var midY = height / 2f;
        var progressX = (float)(vm.Progress * width);

        var levels = vm.Rms;
        if (levels is null || levels.Count == 0)
        {
            DrawPlainProgress(canvas, width, height, progressX);
        }
        else
        {
            DrawBars(canvas, levels, width, height, midY, progressX);
        }

        // Play-head cursor
        canvas.StrokeColor = ThemePalette.Cursor;
        canvas.StrokeSize = 2;
        canvas.DrawLine(progressX, 4, progressX, height - 4);
    }

    static void DrawBars(ICanvas canvas, IReadOnlyList<float> levels, float width, float height, float midY, float progressX)
    {
        const float barWidth = 3f;
        const float gap = 1f;
        var slot = barWidth + gap;
        var barCount = Math.Max(1, (int)(width / slot));
        var perBar = Math.Max(1, levels.Count / barCount);
        var maxBarHeight = height - 8f;

        for (var b = 0; b < barCount; b++)
        {
            // Down-sample: the tallest window in this bucket represents the bar.
            var start = b * perBar;
            if (start >= levels.Count) break;

            var peak = 0f;
            for (var i = start; i < start + perBar && i < levels.Count; i++)
                peak = Math.Max(peak, levels[i]);

            var barHeight = Math.Max(2f, peak * maxBarHeight);
            var x = b * slot;
            canvas.FillColor = (x + barWidth) <= progressX ? ThemePalette.Accent : ThemePalette.Dim;
            canvas.FillRoundedRectangle(x, midY - barHeight / 2f, barWidth, barHeight, 1.5f);
        }
    }

    static void DrawPlainProgress(ICanvas canvas, float width, float height, float progressX)
    {
        var barY = height / 2f - 3f;
        canvas.FillColor = ThemePalette.Dim;
        canvas.FillRoundedRectangle(0, barY, width, 6, 3);
        canvas.FillColor = ThemePalette.Accent;
        canvas.FillRoundedRectangle(0, barY, Math.Max(0, progressX), 6, 3);
    }
}

/// <summary>
/// A pair of segmented LED-style VU meters (RMS and PEAK) driven by the current window of the analysis.
/// </summary>
sealed class VuMeterDrawable(WaveformViewModel vm) : IDrawable
{
    const int Segments = 22;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var colWidth = dirtyRect.Width / 2f;
        DrawMeter(canvas, new RectF(dirtyRect.X, dirtyRect.Y, colWidth, dirtyRect.Height), vm.VuRms);
        DrawMeter(canvas, new RectF(dirtyRect.X + colWidth, dirtyRect.Y, colWidth, dirtyRect.Height), vm.VuPeak);
    }

    static void DrawMeter(ICanvas canvas, RectF area, float level)
    {
        var padding = 10f;
        var segGap = 3f;
        var meterWidth = area.Width - padding * 2;
        var segHeight = (area.Height - (Segments - 1) * segGap) / Segments;

        for (var i = 0; i < Segments; i++)
        {
            var fraction = (i + 0.5f) / Segments;                 // this segment's position on the meter
            var lit = level >= (float)i / Segments;

            Color color;
            if (fraction < 0.6f) color = ThemePalette.VuLow;
            else if (fraction < 0.85f) color = ThemePalette.VuMid;
            else color = ThemePalette.VuHigh;

            // segments fill bottom-up
            var y = area.Y + area.Height - (i + 1) * segHeight - i * segGap;
            canvas.FillColor = lit ? color : ThemePalette.Dim.WithAlpha(0.35f);
            canvas.FillRoundedRectangle(area.X + padding, y, meterWidth, segHeight, 2);
        }
    }
}
