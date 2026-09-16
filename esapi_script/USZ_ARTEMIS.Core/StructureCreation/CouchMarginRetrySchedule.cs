using System;
using System.Collections.Generic;

namespace USZ_ARTEMIS.Core.StructureCreation
{
    public static class CouchMarginRetrySchedule
    {
        public static IReadOnlyList<double> Create(
            double initialMarginMm,
            double incrementMm,
            double maximumMarginMm)
        {
            if (double.IsNaN(initialMarginMm) || double.IsInfinity(initialMarginMm) || initialMarginMm < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialMarginMm));
            }

            if (double.IsNaN(incrementMm) || double.IsInfinity(incrementMm) || incrementMm <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(incrementMm));
            }

            if (double.IsNaN(maximumMarginMm) ||
                double.IsInfinity(maximumMarginMm) ||
                maximumMarginMm < initialMarginMm)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumMarginMm));
            }

            var retryMargins = new List<double>();
            double currentMarginMm = initialMarginMm;

            while (currentMarginMm < maximumMarginMm)
            {
                currentMarginMm = Math.Min(currentMarginMm + incrementMm, maximumMarginMm);
                retryMargins.Add(currentMarginMm);
            }

            return retryMargins;
        }
    }
}
