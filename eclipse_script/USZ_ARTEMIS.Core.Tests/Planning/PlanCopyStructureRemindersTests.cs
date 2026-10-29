using System.Linq;
using USZ_ARTEMIS.Tools;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.Planning;

public sealed class PlanCopyStructureRemindersTests
{
    [Fact]
    public void Unrelated_or_absent_structures_do_not_prompt()
    {
        foreach (var ids in new[] { System.Array.Empty<string>(), new[] { "BODY", "PTV", "HighCheck_Ph" } })
        {
            var reminders = PlanCopyStructureReminders.Create(ids);

            Assert.Empty(reminders.AvoidEntryDoseReminder);
            Assert.Empty(reminders.MaterialOverrideReminder);
        }
    }

    [Fact]
    public void Hip_water_ph_requires_manual_avoid_entry_dose_and_water_verification()
    {
        var reminders = PlanCopyStructureReminders.Create(new[] { "OR_Hip_Water_Ph" });

        Assert.Contains("Manually enable 'Avoid entry dose'", reminders.AvoidEntryDoseReminder);
        Assert.Contains("'OR_Hip_Water_Ph'", reminders.AvoidEntryDoseReminder);
        Assert.Contains("during plan optimization", reminders.AvoidEntryDoseReminder);
        Assert.Contains("'OR_Hip_Water_Ph': material override must be 'Water'", reminders.MaterialOverrideReminder);
        Assert.DoesNotContain("Titanium Alloy", reminders.MaterialOverrideReminder);
    }

    [Theory]
    [InlineData("OR_Hip_Water", "Water")]
    [InlineData("HighDensity_Ph", "Titanium Alloy")]
    public void Material_reminders_are_independent_of_avoid_entry_dose(string id, string material)
    {
        var reminders = PlanCopyStructureReminders.Create(new[] { id });

        Assert.Empty(reminders.AvoidEntryDoseReminder);
        Assert.Contains($"'{id}': material override must be '{material}'", reminders.MaterialOverrideReminder);
        Assert.Contains("cannot automatically verify named material overrides through ESAPI 19.0", reminders.MaterialOverrideReminder);
        Assert.Contains("manually verify", reminders.MaterialOverrideReminder);
    }

    [Fact]
    public void All_present_targets_are_listed_once_without_changing_input()
    {
        var ids = new[] { "OR_Hip_Water", "OR_Hip_Water_Ph", "HighDensity_Ph", "OR_Hip_Water" };
        var originalIds = (string[])ids.Clone();

        var reminders = PlanCopyStructureReminders.Create(ids);

        Assert.Contains("- 'OR_Hip_Water': material override must be 'Water'.", reminders.MaterialOverrideReminder);
        Assert.Contains("- 'OR_Hip_Water_Ph': material override must be 'Water'.", reminders.MaterialOverrideReminder);
        Assert.Contains("- 'HighDensity_Ph': material override must be 'Titanium Alloy'.", reminders.MaterialOverrideReminder);
        Assert.Equal(3, reminders.MaterialOverrideReminder.Split('\n').Count(line => line.StartsWith("- ")));
        Assert.Equal(originalIds, ids);
    }

    [Fact]
    public void Structure_ids_are_matched_without_case_sensitivity()
    {
        var reminders = PlanCopyStructureReminders.Create(new[] { "or_hip_water_ph", "highdensity_ph" });

        Assert.NotEmpty(reminders.AvoidEntryDoseReminder);
        Assert.Contains("Water", reminders.MaterialOverrideReminder);
        Assert.Contains("Titanium Alloy", reminders.MaterialOverrideReminder);
    }

    [Fact]
    public void Similar_structure_ids_do_not_trigger_reminders()
    {
        var reminders = PlanCopyStructureReminders.Create(new[]
        {
            "OR_Hip_Water_Ph_old", "OR_Hip_Water_old", "HighDensity_Ph_inPTV"
        });

        Assert.Empty(reminders.AvoidEntryDoseReminder);
        Assert.Empty(reminders.MaterialOverrideReminder);
    }
}
