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
