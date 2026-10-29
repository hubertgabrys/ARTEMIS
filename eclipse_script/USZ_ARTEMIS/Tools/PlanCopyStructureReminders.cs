using System;
using System.Collections.Generic;

namespace USZ_ARTEMIS.Tools
{
    internal sealed class PlanCopyStructureReminders
    {
        internal string AvoidEntryDoseReminder { get; private set; } = string.Empty;
        internal string MaterialOverrideReminder { get; private set; } = string.Empty;

        internal static PlanCopyStructureReminders Create(IEnumerable<string> structureIds)
        {
            var ids = new HashSet<string>(structureIds, StringComparer.OrdinalIgnoreCase);
            var materialChecks = new List<string>();
            foreach (string waterStructureId in new[] { "OR_Hip_Water", "OR_Hip_Water_Ph" })
            {
                if (ids.Contains(waterStructureId))
                    materialChecks.Add($"- '{waterStructureId}': material override must be 'Water'.");
            }

            if (ids.Contains("HighDensity_Ph"))
                materialChecks.Add("- 'HighDensity_Ph': material override must be 'Titanium Alloy'.");

            return new PlanCopyStructureReminders
            {
                AvoidEntryDoseReminder = ids.Contains("OR_Hip_Water_Ph")
                    ? "The copied plan's structure set contains 'OR_Hip_Water_Ph'.\n\n" +
                      "Manually enable 'Avoid entry dose' for this structure during plan optimization."
                    : string.Empty,
                // ESAPI 19.0 Structure exposes HU overrides, but no named material getter.
                // A matching HU value or material table cannot verify a material override.
                MaterialOverrideReminder = materialChecks.Count > 0
                    ? "ARTEMIS cannot automatically verify named material overrides through ESAPI 19.0.\n\n" +
                      "On the copied plan's structure set, manually verify the following material " +
                      "overrides in Eclipse before optimization or dose calculation:\n\n" +
                      string.Join("\n", materialChecks)
                    : string.Empty
            };
        }
    }
}
