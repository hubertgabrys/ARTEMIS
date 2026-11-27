using System;
using USZ_ARTEMIS.Tools;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.Planning;

public sealed class PlanCopyMaterialTableTests
{
    [Fact]
    public void Compatible_table_is_checked_before_assignment_and_read_back()
    {
        string table = "PreviousTable";
        int reads = 0;
        int writes = 0;
        Assert.Equal("AcurosXB-13.5", PlanCopyMaterialTable.TableName);

        Assert.Empty(PlanCopyMaterialTable.CheckCompatibility(() => table,
            (string requested, out string error) =>
            {
                Assert.Equal("AcurosXB-13.5", requested);
                Assert.Equal(0, writes);
                error = string.Empty;
                return true;
            }));
        Assert.Empty(PlanCopyMaterialTable.Assign(
            () => { reads++; return table; },
            requested => { writes++; table = requested; }));

        Assert.Equal("AcurosXB-13.5", table);
        Assert.Equal(1, writes);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void Already_correct_table_needs_no_compatibility_check_or_write()
    {
        Assert.Empty(PlanCopyMaterialTable.CheckCompatibility(
            () => "AcurosXB-13.5",
            (string requested, out string error) => throw new Exception("Unexpected check")));
        Assert.Empty(PlanCopyMaterialTable.Assign(
            () => "AcurosXB-13.5", _ => throw new Exception("Unexpected assignment")));
    }

    [Theory]
    [InlineData("Table not available", "Table not available")]
    [InlineData("", "Eclipse rejected the physical material table.")]
    public void Rejected_table_returns_a_warning(string reason, string expected)
    {
        Assert.Equal(expected, PlanCopyMaterialTable.CheckCompatibility(
            () => "PreviousTable",
            (string requested, out string error) => { error = reason; return false; }));
    }

    [Fact]
    public void Compatibility_exception_returns_a_warning()
    {
        string warning = PlanCopyMaterialTable.CheckCompatibility(
            () => "PreviousTable",
            (string requested, out string error) => throw new InvalidOperationException("Check failed"));

        Assert.Contains("Check failed", warning);
    }

    [Fact]
    public void Initial_read_exception_returns_a_warning_without_assignment()
    {
        Func<string> read = () => throw new InvalidOperationException("Read failed");
        Assert.Contains("Read failed", PlanCopyMaterialTable.CheckCompatibility(read,
            (string requested, out string error) => throw new Exception("Unexpected check")));
        Assert.Contains("Read failed", PlanCopyMaterialTable.Assign(read,
            _ => throw new Exception("Unexpected assignment")));
    }

    [Fact]
    public void Assignment_exception_returns_a_warning_without_throwing()
    {
        string table = "PreviousTable";
        string warning = PlanCopyMaterialTable.Assign(() => table,
            _ => throw new InvalidOperationException("Assignment failed"));

        Assert.Contains("Assignment failed", warning);
        Assert.Equal("PreviousTable", table);
    }

    [Theory]
    [InlineData("PreviousTable")]
    [InlineData("")]
    [InlineData(null)]
    public void Readback_mismatch_returns_a_warning(string? actualTable)
    {
        string warning = PlanCopyMaterialTable.Assign(() => actualTable!, _ => { });

        Assert.Contains("verification failed", warning);
        Assert.Contains(actualTable ?? "<none>", warning);
    }

    [Fact]
    public void Readback_exception_returns_a_warning_after_assignment()
    {
        bool assigned = false;
        string warning = PlanCopyMaterialTable.Assign(
            () => assigned ? throw new InvalidOperationException("Readback failed") : "PreviousTable",
            _ => assigned = true);

        Assert.True(assigned);
        Assert.Contains("Readback failed", warning);
    }
}
