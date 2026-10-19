namespace ProcessBus.Core.Presentation;

/// <summary>
/// A six-vector plot has little room for text. Keep label placement deterministic,
/// inside the viewport, and independent of WPF so the engineering readout can be tested.
/// This is presentation geometry only: it never changes measured phasor angles.
/// </summary>
public readonly record struct PhasorLabelAnchor(
    double TipX, double TipY, double Width, double Height, bool Right);

public readonly record struct PhasorLabelPosition(double X, double Y);

public static class PhasorLabelLayout
{
    public static PhasorLabelPosition[] Arrange(
        IReadOnlyList<PhasorLabelAnchor> anchors,
        double width,
        double height,
        double topPadding = 20,
        double bottomPadding = 22,
        double gap = 2)
    {
        var positions = new PhasorLabelPosition[anchors.Count];
        for (var side = 0; side < 2; side++)
        {
            var right = side == 1;
            var indices = new List<int>(anchors.Count);
            for (var i = 0; i < anchors.Count; i++)
                if (anchors[i].Right == right)
                    indices.Add(i);

            // Stable tie-breaker prevents jitter when two vectors share an angle.
            indices.Sort((a, b) =>
            {
                var order = anchors[a].TipY.CompareTo(anchors[b].TipY);
                return order != 0 ? order : a.CompareTo(b);
            });

            var top = Math.Max(0, topPadding);
            var bottom = Math.Max(top, height - Math.Max(0, bottomPadding));
            var cursor = top;
            foreach (var index in indices)
            {
                var label = anchors[index];
                var desiredY = label.TipY - label.Height / 2;
                var y = Math.Max(cursor, Math.Max(top, desiredY));
                positions[index] = new PhasorLabelPosition(
                    Math.Clamp(right ? width - label.Width - 4 : 4, 0, Math.Max(0, width - label.Width)),
                    y);
                cursor = y + label.Height + gap;
            }

            // Resolve bottom overflow backwards while preserving sorted order.
            // The six phasor labels comfortably fit in the normal 210px WPF panel.
            var ceiling = bottom;
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                var index = indices[i];
                var label = anchors[index];
                var position = positions[index];
                var y = Math.Max(top, Math.Min(position.Y, ceiling - label.Height));
                positions[index] = position with { Y = y };
                ceiling = y - gap;
            }
        }

        return positions;
    }
}
