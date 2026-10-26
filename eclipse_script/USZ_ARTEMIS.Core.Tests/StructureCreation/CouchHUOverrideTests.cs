using System;
using System.Collections.Generic;
using System.Linq;
using USZ_ARTEMIS.DataExtraction;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.StructureCreation;

public sealed class CouchHUOverrideTests
{
    [Fact]
    public void Igrt_couch_preserves_surface_override_and_model_defaults()
    {
        var structures = CreateStructures("CouchInterior", "CouchSurface", "CouchRails");

        Apply("Exact_IGRT_Couch_Top_medium", structures);

        Assert.Equal(-432, structures.Single(s => s.Id == "CouchSurface").AssignedHU);
        Assert.Null(structures.Single(s => s.Id == "CouchInterior").AssignedHU);
        Assert.Null(structures.Single(s => s.Id == "CouchRails").AssignedHU);
    }

    [Fact]
    public void Flat_panel_preserves_both_overrides_without_overriding_rails()
    {
        var structures = CreateStructures("CouchInterior", "CouchRails", "CouchSurface");

        Apply("Exact_Couch_Top_with_Flat_panel", structures);

        Assert.Equal(-900, structures.Single(s => s.Id == "CouchSurface").AssignedHU);
        Assert.Equal(-700, structures.Single(s => s.Id == "CouchInterior").AssignedHU);
        Assert.Null(structures.Single(s => s.Id == "CouchRails").AssignedHU);
    }

    [Fact]
    public void Other_models_retain_all_configured_HU_values()
    {
        var structures = CreateStructures("OtherSurface");

        Apply("OtherModel", structures);

        Assert.All(structures, s => Assert.Null(s.AssignedHU));
    }

    [Theory]
    [InlineData("CouchSurface")]
    [InlineData("CouchSurface", "CouchInterior", "CouchInterior")]
    [InlineData("CouchInterior", "RenamedSurface")]
    public void Missing_or_ambiguous_targets_fail_before_any_HU_is_changed(params string[] ids)
    {
        var structures = CreateStructures(ids);

        var exception = Assert.Throws<InvalidOperationException>(
            () => Apply("Exact_Couch_Top_with_Flat_panel", structures));

        Assert.Contains("Expected exactly one new couch structure", exception.Message);
        Assert.All(structures, s => Assert.Null(s.AssignedHU));
    }

    [Fact]
    public void HU_assignment_failure_is_propagated_to_couch_creation_for_rollback()
    {
        var failure = new InvalidOperationException("HU assignment failed");
        var structures = CreateStructures("CouchSurface");

        var exception = Assert.Throws<InvalidOperationException>(
            () => CouchDataExtractor.ApplyCouchHUOverrides(
                "Exact_IGRT_Couch_Top_medium", structures, s => s.Id, (_, _) => throw failure));

        Assert.Same(failure, exception);
    }

    private static List<CouchStructure> CreateStructures(params string[] ids) =>
        ids.Select(id => new CouchStructure(id)).ToList();

    private static void Apply(string model, IEnumerable<CouchStructure> structures) =>
        CouchDataExtractor.ApplyCouchHUOverrides(model, structures, s => s.Id, (s, hu) => s.AssignedHU = hu);

    private sealed class CouchStructure(string id)
    {
        public string Id { get; } = id;
        public int? AssignedHU { get; set; }
    }
}
