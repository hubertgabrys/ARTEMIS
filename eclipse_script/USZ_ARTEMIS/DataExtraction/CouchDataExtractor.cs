using System;
using System.Collections.Generic;
using System.Linq;

namespace USZ_ARTEMIS.DataExtraction
{
    class CouchDataExtractor
    {
        public const string IgrtMediumModelName = "Exact IGRT Couch, medium";

        public static Dictionary<string, int> GetCouchHUOverrides(string couchModel)
        {
            // Omitted structures retain the HU values configured in the Eclipse model.
            switch (couchModel)
            {
                case IgrtMediumModelName:
                    return new Dictionary<string, int> { { "CouchSurface", -432 } };
                case "Exact_Couch_Top_with_Flat_panel":
                    return new Dictionary<string, int>
                    {
                        { "CouchSurface", -900 },
                        { "CouchInterior", -700 }
                    };
                default:
                    return new Dictionary<string, int>();
            }
        }

        public static void ApplyCouchHUOverrides<T>(
            string couchModel,
            IEnumerable<T> addedStructures,
            Func<T, string> getStructureId,
            Action<T, int> setAssignedHU)
        {
            var structures = addedStructures.ToList();
            var assignments = new List<KeyValuePair<T, int>>();
            foreach (var huOverride in GetCouchHUOverrides(couchModel))
            {
                var matches = structures.Where(s => getStructureId(s) == huOverride.Key).ToList();
                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one new couch structure '{huOverride.Key}' to apply its HU override; " +
                        $"found {matches.Count}.");
                }

                assignments.Add(new KeyValuePair<T, int>(matches[0], huOverride.Value));
            }

            // Validate every target before changing any assigned HU value.
            foreach (var assignment in assignments)
            {
                setAssignedHU(assignment.Key, assignment.Value);
            }
        }

    }
}
