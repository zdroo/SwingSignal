using SwingSignal.Application.Odds;

namespace SwingSignal.Tests.Odds;

public class OddsMathTests
{
    [Fact]
    public void EffectiveSampleSize_EqualWeights_EqualsCount()
    {
        // (4*0.5)^2 / (4*0.25) = 4 / 1 = 4
        Assert.Equal(4.0, OddsMath.EffectiveSampleSize([0.5, 0.5, 0.5, 0.5]), precision: 12);
    }

    [Fact]
    public void EffectiveSampleSize_OneDominantWeight_CollapsesTowardOne()
    {
        // (1 + 0.01*9)^2 / (1 + 0.0001*9) = 1.09^2 / 1.0009 = 1.1874...
        var weights = new List<double> { 1.0 };
        weights.AddRange(Enumerable.Repeat(0.01, 9));

        Assert.Equal(1.09 * 1.09 / 1.0009, OddsMath.EffectiveSampleSize(weights), precision: 12);
    }

    [Fact]
    public void EffectiveSampleSize_EmptyOrZeroWeights_ReturnsZero()
    {
        Assert.Equal(0.0, OddsMath.EffectiveSampleSize([]));
        Assert.Equal(0.0, OddsMath.EffectiveSampleSize([0.0, 0.0]));
    }

    [Fact]
    public void AdaptiveShrink_ExactFormula()
    {
        // k = 20/(20+30) = 0.4 => 70 + 0.4*(85-70) = 76
        Assert.Equal(76.0, OddsMath.AdaptiveShrink(85, 70, nEff: 20, prior: 30), precision: 12);
    }

    [Fact]
    public void AdaptiveShrink_ThinSample_StaysNearBaseRate()
    {
        // k = 5/(5+30) = 1/7 => 70 + (1/7)*(85-70) = 72.142857...
        Assert.Equal(70 + 15.0 / 7, OddsMath.AdaptiveShrink(85, 70, nEff: 5, prior: 30), precision: 12);
    }

    [Fact]
    public void AdaptiveShrink_ZeroEvidence_ReturnsBaseRate()
    {
        Assert.Equal(52.0, OddsMath.AdaptiveShrink(90, 52, nEff: 0, prior: 30), precision: 12);
    }
}
