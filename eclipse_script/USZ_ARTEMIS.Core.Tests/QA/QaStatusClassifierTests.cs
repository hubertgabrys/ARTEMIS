using USZ_ARTEMIS.Core.QA;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.QA;

public sealed class QaStatusClassifierTests
{
    [Theory]
    [InlineData(true, QaStatus.Acceptable)]
    [InlineData(false, QaStatus.Error)]
    public void Match_status_distinguishes_identical_and_different_results(
        bool isMatch,
        QaStatus expected)
    {
        Assert.Equal(expected, QaStatusClassifier.ForMatch(isMatch));
    }

    [Theory]
    [InlineData(-20.0, QaStatus.Acceptable)]
    [InlineData(-20.1, QaStatus.Warning)]
    [InlineData(20.0, QaStatus.Acceptable)]
    public void Mmo_status_uses_the_inclusive_lower_boundary(
        double displayedPercentage,
        QaStatus expected)
    {
        Assert.Equal(expected, QaStatusClassifier.ForMmoChange(displayedPercentage));
    }

    [Theory]
    [InlineData(-20.0, QaStatus.Acceptable)]
    [InlineData(20.0, QaStatus.Acceptable)]
    [InlineData(-20.01, QaStatus.Warning)]
    [InlineData(20.01, QaStatus.Warning)]
    public void Symmetric_status_uses_inclusive_boundaries(
        double displayedPercentage,
        QaStatus expected)
    {
        Assert.Equal(expected, QaStatusClassifier.ForSymmetricChange(displayedPercentage));
    }

    [Fact]
    public void Missing_percentage_is_a_warning()
    {
        Assert.Equal(QaStatus.Warning, QaStatusClassifier.ForMmoChange(null));
        Assert.Equal(QaStatus.Warning, QaStatusClassifier.ForSymmetricChange(null));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_percentage_is_a_warning(double displayedPercentage)
    {
        Assert.Equal(QaStatus.Warning, QaStatusClassifier.ForMmoChange(displayedPercentage));
        Assert.Equal(QaStatus.Warning, QaStatusClassifier.ForSymmetricChange(displayedPercentage));
    }

    [Theory]
    [InlineData(0.0, 100.0)]
    [InlineData(100.0, 0.0)]
    public void Zero_target_volume_has_no_percentage_and_is_a_warning(
        double adaptedVolume,
        double originalVolume)
    {
        QaPercentageClassification result =
            QaStatusClassifier.ForTargetVolumes(adaptedVolume, originalVolume, 2);

        Assert.Null(result.DisplayedPercentage);
        Assert.Equal(QaStatus.Warning, result.Status);
    }

    [Fact]
    public void Unmatched_target_volume_has_no_percentage_and_is_a_warning()
    {
        QaPercentageClassification result =
            QaStatusClassifier.ForTargetVolumes(100.0, null, 2);

        Assert.Null(result.DisplayedPercentage);
        Assert.Equal(QaStatus.Warning, result.Status);
    }

    [Theory]
    [InlineData(80.0, -20.00, QaStatus.Acceptable)]
    [InlineData(79.99, -20.01, QaStatus.Warning)]
    [InlineData(120.0, 20.00, QaStatus.Acceptable)]
    [InlineData(120.01, 20.01, QaStatus.Warning)]
    public void Target_volume_status_uses_the_displayed_percentage(
        double adaptedVolume,
        double expectedDisplayedPercentage,
        QaStatus expectedStatus)
    {
        QaPercentageClassification result =
            QaStatusClassifier.ForTargetVolumes(adaptedVolume, 100.0, 2);

        Assert.Equal(expectedDisplayedPercentage, result.DisplayedPercentage);
        Assert.Equal(expectedStatus, result.Status);
    }

    [Theory]
    [InlineData(-20.04, 1, -20.0, QaStatus.Acceptable)]
    [InlineData(-20.06, 1, -20.1, QaStatus.Warning)]
    public void Rounded_mmo_percentage_drives_the_displayed_status(
        double rawPercentage,
        int decimalPlaces,
        double expectedDisplayedPercentage,
        QaStatus expectedStatus)
    {
        double displayedPercentage =
            QaStatusClassifier.RoundPercentageForDisplay(rawPercentage, decimalPlaces);

        Assert.Equal(expectedDisplayedPercentage, displayedPercentage);
        Assert.Equal(expectedStatus, QaStatusClassifier.ForMmoChange(displayedPercentage));
    }

    [Theory]
    [InlineData(-20.004, 2, -20.00, QaStatus.Acceptable)]
    [InlineData(-20.006, 2, -20.01, QaStatus.Warning)]
    [InlineData(20.004, 2, 20.00, QaStatus.Acceptable)]
    [InlineData(20.006, 2, 20.01, QaStatus.Warning)]
    [InlineData(-20.04, 1, -20.0, QaStatus.Acceptable)]
    [InlineData(-20.06, 1, -20.1, QaStatus.Warning)]
    [InlineData(20.04, 1, 20.0, QaStatus.Acceptable)]
    [InlineData(20.06, 1, 20.1, QaStatus.Warning)]
    public void Rounded_symmetric_percentage_drives_the_displayed_status(
        double rawPercentage,
        int decimalPlaces,
        double expectedDisplayedPercentage,
        QaStatus expectedStatus)
    {
        double displayedPercentage =
            QaStatusClassifier.RoundPercentageForDisplay(rawPercentage, decimalPlaces);

        Assert.Equal(expectedDisplayedPercentage, displayedPercentage);
        Assert.Equal(expectedStatus, QaStatusClassifier.ForSymmetricChange(displayedPercentage));
    }
}
