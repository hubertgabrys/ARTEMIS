using USZ_ARTEMIS.Core.Planning;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.Planning;

public sealed class PlanCopyPtvSelectionTests
{
    [Theory]
    [InlineData("PTV1_V1_1a", "PTV")]
    [InlineData("ptv1_v1_1a", "ptv")]
    [InlineData("PtV1_V1_1a", "PtV")]
    public void Current_course_ptvs_are_eligible(string id, string dicomType)
    {
        Assert.True(PlanCopyPtvSelection.IsEligible(id, dicomType, isEmpty: false));
    }

    [Theory]
    [InlineData("VB_PTV1_V1_1a", "PTV")]
    [InlineData("CTV1_V1_1a", "PTV")]
    [InlineData("PTV1_V1_1a", "CTV")]
    [InlineData("", "PTV")]
    [InlineData("PTV1_V1_1a", "")]
    public void Non_current_or_non_ptv_structures_are_not_eligible(
        string id,
        string dicomType)
    {
        Assert.False(PlanCopyPtvSelection.IsEligible(id, dicomType, isEmpty: false));
    }

    [Fact]
    public void Empty_ptv_is_not_eligible()
    {
        Assert.False(PlanCopyPtvSelection.IsEligible("PTV1_V1_1a", "PTV", isEmpty: true));
    }
}
