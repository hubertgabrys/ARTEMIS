using USZ_ARTEMIS.Actions;
using USZ_ARTEMIS.Core.Rules;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.Rules;

public class Ctv1RuleTests
{
    [Fact]
    public void UsesSelectedTargetAndFixedNamesForOtherOutputs()
    {
        RuleStructureUse use = Ctv1Rule.GetStructureUse("AdaptedCTV");

        Assert.Equal(new[] { "Prostate", "SeminalVesicle" }, use.InputIds);
        Assert.Equal(use.InputIds, use.GeometryRequiredInputIds);
        Assert.Equal(
            new[] { "Prostate+1cm_Ph", "SeminalVesicleBase1cm_Ph", "AdaptedCTV", "CTV1_clean" },
            use.OutputIds);
        Assert.Empty(Ctv1Rule.GetTargetIdError("AdaptedCTV"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData(" prostate ")]
    [InlineData("SEMINALVESICLE")]
    [InlineData("Prostate+1cm_Ph")]
    [InlineData("seminalvesiclebase1cm_ph")]
    [InlineData("ctv1_clean")]
    public void RejectsMissingTargetsAndTargetsThatWouldOverwriteAnotherStructure(string? targetId)
    {
        Assert.NotEmpty(Ctv1Rule.GetTargetIdError(targetId!));
    }
}
