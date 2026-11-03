using System;
using System.Windows;
using System.Windows.Media;

namespace NightKey.ViewModels;

/// <summary>
/// Draws the lit part of a moon. fraction 1 = full moon (fresh code), 0 = new moon (code expiring).
/// The terminator is a half-ellipse whose width follows the phase.
/// </summary>
public static class MoonShape
{
    public static Geometry Build(double fraction, double radius, double cx, double cy)
    {
        if (fraction >= 0.995) return Frozen(new EllipseGeometry(new Point(cx, cy), radius, radius));
        if (fraction <= 0.005) return Geometry.Empty;

        double terminator = radius * Math.Abs(1 - 2 * fraction);
        var top = new Point(cx, cy - radius);
        var bottom = new Point(cx, cy + radius);

        var figure = new PathFigure { StartPoint = top, IsClosed = true, IsFilled = true };
        // Lit limb: right half of the disc, top to bottom.
        figure.Segments.Add(new ArcSegment(bottom, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
        // Terminator back to the top: bows right for a crescent, left for a gibbous moon.
        figure.Segments.Add(new ArcSegment(top, new Size(Math.Max(terminator, 0.01), radius), 0, false,
            fraction < 0.5 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true));

        return Frozen(new PathGeometry(new[] { figure }));
    }

    private static Geometry Frozen(Geometry g)
    {
        g.Freeze();
        return g;
    }
}
