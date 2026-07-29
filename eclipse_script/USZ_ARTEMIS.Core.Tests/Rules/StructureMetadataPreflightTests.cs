using System.Collections.Generic;
using USZ_ARTEMIS.Core.Rules;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.Rules;

public sealed class StructureMetadataPreflightTests
{
    [Fact]
    public void MissingOutputIsCreatedFromMatchingReferenceMetadata()
    {
        var reference = Snapshot("Ring_Ph", "AVOIDANCE", "99VMS_STRUCTCODE", "Avoidance");
        var result = Evaluate(
            references: [reference],
            targets: [],
            uses: [Use([], ["ring_ph"])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.CreateFromReference, preparation.Kind);
        Assert.Equal("AVOIDANCE", preparation.ExpectedVolumeType);
        Assert.Equal("ring_ph", preparation.StructureId);
    }

    [Fact]
    public void ExistingOutputWithMatchingVolumeTypeIsPreparedForCodeSynchronization()
    {
        var reference = Snapshot("Bladder", "ORGAN", "SRT", "T-74000");
        var target = Snapshot(" bladder ", "organ", null, null);
        var result = Evaluate(
            references: [reference],
            targets: [target],
            uses: [Use(["Bladder"], ["BLADDER"])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.SynchronizeExisting, preparation.Kind);
    }

    [Fact]
    public void ExistingOutputWithMatchingMetadataRequiresNoPreparation()
    {
        var reference = Snapshot("Bladder", "ORGAN", "SRT", "T-74000");
        var target = Snapshot("Bladder", "organ", "SRT", "T-74000");
        var result = Evaluate(
            references: [reference],
            targets: [target],
            uses: [Use(["Bladder"], ["Bladder"])]);

        Assert.True(result.CanApply);
        Assert.Empty(result.Preparations);
    }

    [Fact]
    public void ExistingOutputWithDifferentColorIsPreparedForSynchronization()
    {
        var reference = Snapshot(
            "Bladder",
            "ORGAN",
            "SRT",
            "T-74000",
            colorArgb: 0xFF102030u);
        var target = Snapshot(
            "Bladder",
            "organ",
            "SRT",
            "T-74000",
            colorArgb: 0xFF405060u);
        var result = Evaluate(
            references: [reference],
            targets: [target],
            uses: [Use(["Bladder"], ["Bladder"])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.SynchronizeExisting, preparation.Kind);
    }

    [Fact]
    public void ExistingOutputCodeIsSynchronizedWhenBaseCodeIsEmpty()
    {
        var result = Evaluate(
            references: [Snapshot("Bladder", "ORGAN")],
            targets: [Snapshot("Bladder", "ORGAN", "SRT", "T-74000")],
            uses: [Use(["Bladder"], ["Bladder"])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.SynchronizeExisting, preparation.Kind);
    }

    [Fact]
    public void ExistingOutputWithDifferentVolumeTypeBlocksApplication()
    {
        var result = Evaluate(
            references: [Snapshot("Bladder", "ORGAN")],
            targets: [Snapshot("Bladder", "CONTROL")],
            uses: [Use(["Bladder"], ["Bladder"])]);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("Volume Type mismatch"));
    }

    [Fact]
    public void PermanentOutputWithoutReferenceBlocksApplication()
    {
        var result = Evaluate(
            references: [],
            targets: [],
            uses: [Use([], ["Unexpected_Ph"])]);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("no matching structure"));
    }

    [Fact]
    public void TemporaryOutputWithoutReferenceUsesControlFallback()
    {
        var result = StructureMetadataPreflight.Evaluate(
            referenceStructures: [],
            targetStructures: [],
            orderedRuleUses: [Use([], ["RectalWallHelp_Ph"])],
            structureIdsRemovedBeforeRules: [],
            temporaryOutputIds: ["rectalwallhelp_ph"]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.CreateTemporary, preparation.Kind);
        Assert.Equal("CONTROL", preparation.ExpectedVolumeType);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void RemovedExistingOutputIsRecreatedFromReference()
    {
        var result = StructureMetadataPreflight.Evaluate(
            referenceStructures: [Snapshot("Ring_Ph", "AVOIDANCE")],
            targetStructures: [Snapshot("Ring_Ph", "CONTROL")],
            orderedRuleUses: [Use([], ["Ring_Ph"])],
            structureIdsRemovedBeforeRules: ["ring_ph"],
            temporaryOutputIds: []);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(StructureMetadataPreparationKind.CreateFromReference, preparation.Kind);
        Assert.Equal("AVOIDANCE", preparation.ExpectedVolumeType);
    }

    [Fact]
    public void ExistingEmptyInputIsAvailableWithoutPreparation()
    {
        var result = Evaluate(
            references: [Snapshot("Bowel", "ORGAN")],
            targets: [Snapshot("Bowel", "ORGAN", isEmpty: true)],
            uses: [Use(["Bowel"], [])]);

        Assert.True(result.CanApply);
        Assert.Empty(result.Preparations);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ExistingEmptyCriticalInputNotUsedForMarginIsAvailable()
    {
        var result = Evaluate(
            references: [Snapshot("CTV1", "CTV")],
            targets: [Snapshot("CTV1", "CTV", isEmpty: true)],
            uses: [Use(["CTV1"], [])]);

        Assert.True(result.CanApply);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("GTV")]
    [InlineData("CTV")]
    [InlineData("PTV")]
    [InlineData("ITV")]
    [InlineData("EXTERNAL")]
    public void ExistingEmptyCriticalMarginInputBlocksApplication(string volumeType)
    {
        var result = Evaluate(
            references: [Snapshot("Required", volumeType)],
            targets: [Snapshot("Required", volumeType, isEmpty: true)],
            uses: [Use(["Required"], [], ["Required"])]);

        Assert.False(result.CanApply);
        Assert.Contains(
            result.Errors,
            error => error.Contains("used for margin generation"));
    }

    [Fact]
    public void ExistingEmptyOrganMarginInputIsAvailable()
    {
        var result = Evaluate(
            references: [Snapshot("Bowel", "ORGAN")],
            targets: [Snapshot("Bowel", "ORGAN", isEmpty: true)],
            uses: [Use(["Bowel"], [], ["Bowel"])]);

        Assert.True(result.CanApply);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingNonTargetInputIsCreatedEmptyFromReferenceMetadata()
    {
        var result = Evaluate(
            references: [Snapshot("Bowel", "ORGAN", "SRT", "T-D4000")],
            targets: [],
            uses: [Use(["bowel"], [])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(
            StructureMetadataPreparationKind.CreateEmptyInputFromReference,
            preparation.Kind);
        Assert.Equal("ORGAN", preparation.ExpectedVolumeType);
        Assert.Equal("bowel", preparation.StructureId);
        Assert.Contains(result.Warnings, warning => warning.Contains("created empty"));
    }

    [Fact]
    public void RepeatedMissingInputIsPreparedOnlyOnce()
    {
        var result = Evaluate(
            references:
            [
                Snapshot("Bowel", "ORGAN"),
                Snapshot("Combined1_Ph", "CONTROL"),
                Snapshot("Combined2_Ph", "CONTROL")
            ],
            targets: [],
            uses:
            [
                Use(["Bowel"], ["Combined1_Ph"]),
                Use(["Bowel"], ["Combined2_Ph"])
            ]);

        Assert.True(result.CanApply);
        Assert.Single(
            result.Preparations,
            preparation =>
                preparation.Kind ==
                StructureMetadataPreparationKind.CreateEmptyInputFromReference);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void MissingInputWithoutReferenceBlocksApplication()
    {
        var result = Evaluate(
            references: [],
            targets: [],
            uses: [Use(["Bowel"], [])]);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("no matching structure"));
        Assert.Empty(result.Preparations);
    }

    [Fact]
    public void MissingInputWithUnknownReferenceVolumeTypeBlocksApplication()
    {
        var result = Evaluate(
            references: [Snapshot("Bowel", " ")],
            targets: [],
            uses: [Use(["Bowel"], [])]);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("no Volume Type"));
        Assert.Empty(result.Preparations);
    }

    [Fact]
    public void MissingNonTargetInputThatIsAlsoCurrentOutputIsCreatedEmpty()
    {
        var result = Evaluate(
            references:
            [
                Snapshot("Sigma", "ORGAN", "SRT", "T-59300"),
                Snapshot("CTV1", "CTV")
            ],
            targets: [Snapshot("CTV1", "CTV")],
            uses: [Use(["Sigma", "CTV1"], ["Sigma"])]);

        Assert.True(result.CanApply);
        var preparation = Assert.Single(result.Preparations);
        Assert.Equal(
            StructureMetadataPreparationKind.CreateEmptyInputFromReference,
            preparation.Kind);
        Assert.Equal("Sigma", preparation.StructureId);
        Assert.Equal("ORGAN", preparation.ExpectedVolumeType);
        Assert.Contains(result.Warnings, warning => warning.Contains("created empty"));
    }

    [Fact]
    public void MissingSelfDependentSourceConsumedBeforeItsRuleIsCreatedEmpty()
    {
        var result = Evaluate(
            references:
            [
                Snapshot("Sigma", "ORGAN", "SRT", "T-59300"),
                Snapshot("CTV1", "CTV"),
                Snapshot("Combined_Ph", "CONTROL")
            ],
            targets: [Snapshot("CTV1", "CTV")],
            uses:
            [
                Use(["Sigma"], ["Combined_Ph"]),
                Use(["Sigma", "CTV1"], ["Sigma"])
            ]);

        Assert.True(result.CanApply);
        Assert.Equal(2, result.Preparations.Count);
        var preparation = Assert.Single(
            result.Preparations,
            candidate =>
                candidate.Kind ==
                StructureMetadataPreparationKind.CreateEmptyInputFromReference);
        Assert.Equal("Sigma", preparation.StructureId);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void MissingSafetyCriticalInputThatIsAlsoCurrentOutputIsNotCreatedEmpty()
    {
        var result = Evaluate(
            references: [Snapshot("CTV1", "CTV")],
            targets: [],
            uses: [Use(["CTV1"], ["CTV1"])]);

        Assert.False(result.CanApply);
        Assert.Contains(
            result.Errors,
            error => error.Contains("not created empty automatically"));
        Assert.DoesNotContain(
            result.Preparations,
            preparation =>
                preparation.Kind ==
                StructureMetadataPreparationKind.CreateEmptyInputFromReference);
    }

    [Theory]
    [InlineData("GTV")]
    [InlineData("CTV")]
    [InlineData("PTV")]
    [InlineData("ITV")]
    [InlineData("TREATED_VOLUME")]
    [InlineData("IRRAD_VOLUME")]
    [InlineData("EXTERNAL")]
    public void MissingSafetyCriticalInputIsNotCreatedEmpty(string volumeType)
    {
        var result = Evaluate(
            references: [Snapshot("Required", volumeType)],
            targets: [],
            uses: [Use(["Required"], [])]);

        Assert.False(result.CanApply);
        Assert.Contains(
            result.Errors,
            error => error.Contains("not created empty automatically"));
        Assert.Empty(result.Preparations);
    }

    [Fact]
    public void InputRemovedBeforeRulesMustBeProducedBeforeItIsConsumed()
    {
        var reference = Snapshot("Intermediate_Ph", "CONTROL");
        var target = Snapshot("Intermediate_Ph", "CONTROL");
        var result = StructureMetadataPreflight.Evaluate(
            referenceStructures: [reference],
            targetStructures: [target],
            orderedRuleUses:
            [
                Use(["Intermediate_Ph"], ["Final_Ph"]),
                Use([], ["Intermediate_Ph"])
            ],
            structureIdsRemovedBeforeRules: ["intermediate_ph"],
            temporaryOutputIds: []);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("removed before"));
        Assert.DoesNotContain(
            result.Preparations,
            preparation =>
                preparation.Kind ==
                StructureMetadataPreparationKind.CreateEmptyInputFromReference);
    }

    [Fact]
    public void AbsentGeneratedInputMustBeProducedBeforeItIsConsumed()
    {
        var result = Evaluate(
            references:
            [
                Snapshot("Intermediate_Ph", "CONTROL"),
                Snapshot("Final_Ph", "CONTROL")
            ],
            targets: [],
            uses:
            [
                Use(["Intermediate_Ph"], ["Final_Ph"]),
                Use([], ["Intermediate_Ph"])
            ]);

        Assert.False(result.CanApply);
        Assert.Contains(
            result.Errors,
            error => error.Contains("not produced by an earlier rule"));
        Assert.DoesNotContain(
            result.Preparations,
            preparation =>
                preparation.Kind ==
                StructureMetadataPreparationKind.CreateEmptyInputFromReference);
    }

    [Fact]
    public void OutputProducedEarlierCanBeConsumedByLaterRule()
    {
        var result = Evaluate(
            references:
            [
                Snapshot("Intermediate_Ph", "CONTROL"),
                Snapshot("Final_Ph", "CONTROL")
            ],
            targets: [],
            uses:
            [
                Use([], ["Intermediate_Ph"]),
                Use(["Intermediate_Ph"], ["Final_Ph"])
            ]);

        Assert.True(result.CanApply);
        Assert.Equal(2, result.Preparations.Count);
    }

    [Fact]
    public void ApprovedExistingOutputBlocksApplication()
    {
        var result = Evaluate(
            references: [Snapshot("Rectum", "ORGAN")],
            targets: [Snapshot("Rectum", "ORGAN", isApproved: true)],
            uses: [Use(["Rectum"], ["Rectum"])]);

        Assert.False(result.CanApply);
        Assert.Contains(result.Errors, error => error.Contains("approved"));
    }

    private static StructureMetadataPreflightResult Evaluate(
        IEnumerable<StructureMetadataSnapshot> references,
        IEnumerable<StructureMetadataSnapshot> targets,
        IEnumerable<RuleStructureUse> uses)
    {
        return StructureMetadataPreflight.Evaluate(
            references,
            targets,
            uses,
            structureIdsRemovedBeforeRules: [],
            temporaryOutputIds: []);
    }

    private static RuleStructureUse Use(
        IEnumerable<string> inputs,
        IEnumerable<string> outputs,
        IEnumerable<string>? geometryRequiredInputs = null)
    {
        return new RuleStructureUse(
            inputs,
            outputs,
            geometryRequiredInputs);
    }

    private static StructureMetadataSnapshot Snapshot(
        string id,
        string volumeType,
        string? scheme = null,
        string? code = null,
        bool isApproved = false,
        bool isEmpty = false,
        uint colorArgb = 0)
    {
        return new StructureMetadataSnapshot(
            id,
            volumeType,
            scheme,
            code,
            isApproved,
            isEmpty,
            colorArgb);
    }
}
