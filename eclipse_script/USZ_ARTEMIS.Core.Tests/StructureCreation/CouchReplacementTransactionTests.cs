using System;
using USZ_ARTEMIS.Core.StructureCreation;
using Xunit;

namespace USZ_ARTEMIS.Core.Tests.StructureCreation;

public sealed class CouchReplacementTransactionTests
{
    [Fact]
    public void Successful_replacement_does_not_restore_the_previous_couch()
    {
        bool restorationAttempted = false;

        CouchReplacementTransaction.Execute(
            markPreviousCouchRemoved => markPreviousCouchRemoved(),
            () => restorationAttempted = true);

        Assert.False(restorationAttempted);
    }

    [Fact]
    public void Failure_before_removal_preserves_the_previous_couch_without_restoration()
    {
        bool restorationAttempted = false;

        var exception = Assert.Throws<CouchReplacementException>(
            () => CouchReplacementTransaction.Execute(
                _ => throw new InvalidOperationException("replacement failed"),
                () => restorationAttempted = true));

        Assert.True(exception.PreviousCouchAvailable);
        Assert.False(restorationAttempted);
        Assert.Contains("remains unchanged", exception.Message);
    }

    [Fact]
    public void Failure_after_removal_restores_the_previous_couch()
    {
        bool restorationAttempted = false;

        var exception = Assert.Throws<CouchReplacementException>(
            () => CouchReplacementTransaction.Execute(
                markPreviousCouchRemoved =>
                {
                    markPreviousCouchRemoved();
                    throw new InvalidOperationException("replacement failed");
                },
                () => restorationAttempted = true));

        Assert.True(exception.PreviousCouchAvailable);
        Assert.True(restorationAttempted);
        Assert.Contains("was restored", exception.Message);
    }

    [Fact]
    public void Failed_restoration_discloses_that_the_couch_may_be_missing()
    {
        var exception = Assert.Throws<CouchReplacementException>(
            () => CouchReplacementTransaction.Execute(
                markPreviousCouchRemoved =>
                {
                    markPreviousCouchRemoved();
                    throw new InvalidOperationException("replacement failed");
                },
                () => throw new InvalidOperationException("restoration failed")));

        Assert.False(exception.PreviousCouchAvailable);
        Assert.Contains("may be missing", exception.Message);
        var aggregate = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
    }
}
