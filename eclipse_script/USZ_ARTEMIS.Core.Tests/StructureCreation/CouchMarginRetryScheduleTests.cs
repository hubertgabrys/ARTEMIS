using System;
using USZ_ARTEMIS.Core.StructureCreation;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.StructureCreation;

public sealed class CouchMarginRetryScheduleTests
{
    [Fact]
    public void Retry_margins_increase_by_five_millimeters_up_to_the_maximum()
    {
        var margins = CouchMarginRetrySchedule.Create(5, 5, 50);

        Assert.Equal(new double[] { 10, 15, 20, 25, 30, 35, 40, 45, 50 }, margins);
    }

    [Fact]
    public void Final_retry_is_capped_at_the_maximum()
    {
        var margins = CouchMarginRetrySchedule.Create(12.5, 5, 50);

        Assert.Equal(new double[] { 17.5, 22.5, 27.5, 32.5, 37.5, 42.5, 47.5, 50 }, margins);
    }

    [Fact]
    public void No_retries_are_returned_when_initial_margin_is_the_maximum()
    {
        var margins = CouchMarginRetrySchedule.Create(50, 5, 50);

        Assert.Empty(margins);
    }

    [Theory]
    [InlineData(-1, 5, 50)]
    [InlineData(5, 0, 50)]
    [InlineData(51, 5, 50)]
    public void Invalid_arguments_are_rejected(
        double initialMarginMm,
        double incrementMm,
        double maximumMarginMm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CouchMarginRetrySchedule.Create(initialMarginMm, incrementMm, maximumMarginMm));
    }
}
