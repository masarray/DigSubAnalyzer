using ProcessBus.Core.Models;
using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Deterministic layout / live-render contracts: no UI-side electrical assumptions
/// and no dependence on Windows/Npcap to verify the narrow phasor panel.
/// </summary>
public sealed class PhasorPresentationContractTests
{
    private static ChannelValueModel Channel(string name, double rms, double angle, double instant = 0) =>
        new(name) { Unit = name.StartsWith('U') ? "V" : "A", RmsValue = rms,
            AngleDegrees = angle, InstantValue = instant };

    private static AnalogValuesSnapshot Values(
        double iaAngle = -30, double iaRms = 0.5, double iaInstant = 0) =>
        new()
        {
            Ua = Channel("Ua", 57.73, 0),
            Ub = Channel("Ub", 57.73, -120),
            Uc = Channel("Uc", 57.73, 120),
            Ia = Channel("Ia", iaRms, iaAngle, iaInstant),
            Ib = Channel("Ib", 1, -150),
            Ic = Channel("Ic", 1, 90)
        };

    [Fact]
    public void InstantaneousSampleChangesDoNotForceFullPhasorRepaint()
    {
        var first = Values(iaInstant: 0.1);
        var next = Values(iaInstant: 0.2);
        Assert.False(LiveRenderCoalescer.SameAnalog(first, next));
        Assert.True(LiveRenderCoalescer.SamePhasors(first, next));
    }

    [Fact]
    public void AnyMeasuredPhaseOrRmsChangeInvalidatesPhasorImmediately()
    {
        var original = Values();
        Assert.False(LiveRenderCoalescer.SamePhasors(original, Values(iaAngle: -29.999)));
        Assert.False(LiveRenderCoalescer.SamePhasors(original, Values(iaRms: 0.500001)));
        // Current C is independently measured; it must never borrow voltage C.
        Assert.NotEqual(original.Ic.AngleDegrees, original.Uc.AngleDegrees);
    }

    [Fact]
    public void MissingPhasorNeverBecomesAnInventedNominalAngle()
    {
        var original = Values();
        var missing = new AnalogValuesSnapshot
        {
            Ua = original.Ua, Ub = original.Ub, Uc = original.Uc,
            Ia = original.Ia, Ib = original.Ib,
            Ic = new ChannelValueModel("Ic") { RmsValue = 0, Unit = "A" }
        };
        Assert.False(LiveRenderCoalescer.SamePhasors(original, missing));
        Assert.Null(missing.Ic.AngleDegrees);
    }

    [Theory]
    [InlineData(298, 220)]
    [InlineData(320, 280)]
    [InlineData(215, 210)]
    public void SixCrowdedLabelsStayInsideNarrowViewportAndNeverOverlap(double width, double height)
    {
        // Two co-linear Ua/Ia vectors and clustered Uc/Ic and Ub/Ib labels.
        var anchors = new[]
        {
            new PhasorLabelAnchor(210, 112, 47, 14, true),
            new PhasorLabelAnchor(85, 166, 47, 14, false),
            new PhasorLabelAnchor(85, 65, 47, 14, false),
            new PhasorLabelAnchor(190, 112, 47, 14, true),
            new PhasorLabelAnchor(95, 162, 47, 14, false),
            new PhasorLabelAnchor(95, 69, 47, 14, false)
        };
        var labels = PhasorLabelLayout.Arrange(anchors, width, height);
        Assert.Equal(anchors.Length, labels.Length);

        for (var i = 0; i < labels.Length; i++)
        {
            Assert.InRange(labels[i].X, 0, width - anchors[i].Width);
            Assert.InRange(labels[i].Y, 18, height - anchors[i].Height);
            for (var j = i + 1; j < labels.Length; j++)
            {
                if (anchors[i].Right != anchors[j].Right)
                    continue;
                var firstBottom = labels[i].Y + anchors[i].Height;
                var secondBottom = labels[j].Y + anchors[j].Height;
                Assert.True(firstBottom <= labels[j].Y || secondBottom <= labels[i].Y,
                    $"Overlapping labels {i}/{j} at height {height}.");
            }
        }
    }


    [Theory]
    [InlineData(57.739999, "V", "U max 57.74 V")]
    [InlineData(0.0001234, "A", "U max 0.000123 A")]
    [InlineData(15000, "counts", "U max 15 kcounts")]
    [InlineData(1250000, "V", "U max 1.25 MV")]
    public void PhasorScaleLabelsPreserveNonzeroEvidenceWithoutChangingEngineeringData(
        double rms, string unit, string expected)
    {
        Assert.Equal(expected, PhasorScaleCaption.Format("U", rms, unit));
    }

    [Fact]
    public void MissingOrInvalidRmsNeverCreatesAConvincingScale()
    {
        Assert.Equal("I N/A", PhasorScaleCaption.Format("I", 0, "A"));
        Assert.Equal("I N/A", PhasorScaleCaption.Format("I", double.NaN, "A"));
    }


    [Theory]
    [InlineData(-0.10)]
    [InlineData(0.00)]
    [InlineData(0.10)]
    public void PhaseAVoltageAndCurrentCalloutsLandOnTheirOwnDistinctArrowTips(
        double smallVerticalJitter)
    {
        // Physical screenshot: Ua=57.73V ∠0°, Ia=0.50A ∠0.01°,
        // with different normalized radii. Labels share the RIGHT rail.
        const double uaY = 120;
        var iaY = uaY + smallVerticalJitter;
        var anchors = new[]
        {
            new PhasorLabelAnchor(246.4, uaY, 52, 14, true, false), // Ua
            new PhasorLabelAnchor(114, 161, 52, 14, false, false), // Ub
            new PhasorLabelAnchor(114, 78, 52, 14, false, false),  // Uc
            new PhasorLabelAnchor(187.4, iaY, 52, 14, true, true), // Ia
            new PhasorLabelAnchor(124, 152, 52, 14, false, true), // Ib
            new PhasorLabelAnchor(124, 88, 52, 14, false, true)   // Ic
        };
        var positions = PhasorLabelLayout.Arrange(anchors, 320, 245, 18, 42);
        var uaRoute = PhasorCalloutRouting.Route(
            new PhasorCalloutPoint(anchors[0].TipX, anchors[0].TipY),
            positions[0], anchors[0].Width, anchors[0].Height, true);
        var iaRoute = PhasorCalloutRouting.Route(
            new PhasorCalloutPoint(anchors[3].TipX, anchors[3].TipY),
            positions[3], anchors[3].Width, anchors[3].Height, true);

        Assert.Equal(new PhasorCalloutPoint(246.4, uaY), uaRoute.Tip);
        Assert.Equal(new PhasorCalloutPoint(187.4, iaY), iaRoute.Tip);
        Assert.NotEqual(uaRoute.Tip, iaRoute.Tip);
        Assert.NotEqual(uaRoute.Knee, iaRoute.Knee);
        Assert.NotEqual(uaRoute.LabelEdge, iaRoute.LabelEdge);

        // Long Ia leader still stays connected to its real short current arrow.
        Assert.True(iaRoute.LabelEdge.X - iaRoute.Tip.X > 19);
        Assert.True(iaRoute.Knee.X > iaRoute.Tip.X);
        Assert.Equal(iaRoute.Knee.Y, iaRoute.LabelEdge.Y);
        Assert.True(iaRoute.LabelEdge.Y < uaY - 6,
            "Current leader must leave the shared Ua/Ia axis ABOVE the voltage shaft.");
        Assert.True(uaRoute.LabelEdge.Y > uaY + 5,
            "Voltage leader must enter a lower, separate lane.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalloutRoutingConnectsRealTipsOnEitherSideEvenWithLongLeaders(bool right)
    {
        var tip = new PhasorCalloutPoint(right ? 185 : 100, 100);
        var label = new PhasorLabelPosition(right ? 265 : 4, 58);
        var route = PhasorCalloutRouting.Route(tip, label, 48, 14, right);

        Assert.Equal(tip, route.Tip);
        Assert.Equal(right ? 262 : 55, route.LabelEdge.X);
        Assert.Equal(65, route.LabelEdge.Y);
        Assert.True(right ? route.Knee.X > tip.X : route.Knee.X < tip.X);
        Assert.True(right
            ? route.Tip.X <= route.Knee.X && route.Knee.X <= route.LabelEdge.X
            : route.Tip.X >= route.Knee.X && route.Knee.X >= route.LabelEdge.X);
    }

    [Fact]
    public void ExactlyCollinearTipLabelsStaySeparateAcrossTinySignChanges()
    {
        foreach (var dy in new[] { -0.02, 0.0, 0.02 })
        {
            var anchors = new[]
            {
                new PhasorLabelAnchor(246, 119.99, 48, 14, true, false),
                new PhasorLabelAnchor(187, 119.99 + dy, 48, 14, true, true)
            };
            var labels = PhasorLabelLayout.Arrange(anchors, 310, 215, 18, 42);
            Assert.True(labels[1].Y + anchors[1].Height <= labels[0].Y);
        }
    }

    [Fact]
    public void SixVectorsSharingOneAngleStillReceiveDistinctStableLabelSlots()
    {
        var anchors = Enumerable.Range(0, 6)
            .Select(_ => new PhasorLabelAnchor(210, 110, 45, 15, true))
            .ToArray();
        var labels = PhasorLabelLayout.Arrange(anchors, 290, 210);
        Assert.Equal(6, labels.Select(x => x.Y).Distinct().Count());
        Assert.Equal(labels.OrderBy(x => x.Y).ToArray(), labels);
        Assert.True(labels[^1].Y + 15 <= 188);
    }
}
