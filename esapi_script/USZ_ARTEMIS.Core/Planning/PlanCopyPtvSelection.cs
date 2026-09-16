using System;

namespace USZ_ARTEMIS.Core.Planning
{
    public static class PlanCopyPtvSelection
    {
        public static bool IsEligible(string id, string dicomType, bool isEmpty)
        {
            return !isEmpty &&
                   !string.IsNullOrWhiteSpace(id) &&
                   id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(dicomType) &&
                   dicomType.Equals("PTV", StringComparison.OrdinalIgnoreCase);
        }
    }
}
