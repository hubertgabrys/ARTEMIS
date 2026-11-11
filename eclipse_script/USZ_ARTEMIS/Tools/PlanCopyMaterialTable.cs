using System;

namespace USZ_ARTEMIS.Tools
{
    internal static class PlanCopyMaterialTable
    {
        internal const string TableName = "AcurosXB-13.5";

        internal delegate bool CompatibilityCheck(string tableName, out string error);

        // Return a warning instead of blocking plan copying; assignment can be done manually.
        internal static string CheckCompatibility(
            Func<string> getTable,
            CompatibilityCheck canUseTable)
        {
            try
            {
                if (string.Equals(getTable(), TableName, StringComparison.Ordinal))
                    return string.Empty;

                string error;
                if (!canUseTable(TableName, out error))
                    return string.IsNullOrWhiteSpace(error)
                        ? "Eclipse rejected the physical material table."
                        : error;

                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Could not check the physical material table: " + ex.Message;
            }
        }

        internal static string Assign(Func<string> getTable, Action<string> setTable)
        {
            try
            {
                if (string.Equals(getTable(), TableName, StringComparison.Ordinal))
                    return string.Empty;

                setTable(TableName);
                string actualTable = getTable();
                return string.Equals(actualTable, TableName, StringComparison.Ordinal)
                    ? string.Empty
                    : $"Physical material table verification failed. Eclipse reports '{actualTable ?? "<none>"}'.";
            }
            catch (Exception ex)
            {
                return "Could not assign or verify the physical material table: " + ex.Message;
            }
        }
    }
}
