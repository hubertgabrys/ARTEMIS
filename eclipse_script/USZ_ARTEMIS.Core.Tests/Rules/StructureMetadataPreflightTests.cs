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
        Assert.Contains(result.Errors, error => error.Contains("will not be available"));
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
        IEnumerable<string> outputs)
    {
        return new RuleStructureUse(inputs, outputs);
    }

    private static StructureMetadataSnapshot Snapshot(
        string id,
        string volumeType,
        string? scheme = null,
        string? code = null,
        bool isApproved = false)
    {
        return new StructureMetadataSnapshot(id, volumeType, scheme, code, isApproved);
    }
}
