using System;

namespace USZ_ARTEMIS.Core.QA
{
    public sealed class QaPercentageClassification
    {
        internal QaPercentageClassification(double? displayedPercentage, QaStatus status)
        {
            DisplayedPercentage = displayedPercentage;
            Status = status;
        }

        public double? DisplayedPercentage { get; }

        public QaStatus Status { get; }
    }

    public enum QaStatus
    {
        Acceptable,
        Warning,
        Error
    }

    public static class QaStatusClassifier
    {
        private const double MmoWarningThresholdPercentage = -20.0;
        private const double SymmetricWarningThresholdPercentage = 20.0;

        public static double RoundPercentageForDisplay(double percentage, int decimalPlaces)
        {
            return Math.Round(percentage, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        public static QaStatus ForMatch(bool isMatch)
        {
            return isMatch ? QaStatus.Acceptable : QaStatus.Error;
        }

        public static QaStatus ForMmoChange(double? displayedPercentage)
        {
            if (!IsFinite(displayedPercentage))
            {
                return QaStatus.Warning;
            }

            return displayedPercentage.Value >= MmoWarningThresholdPercentage
                ? QaStatus.Acceptable
                : QaStatus.Warning;
        }

        public static QaStatus ForSymmetricChange(double? displayedPercentage)
        {
            if (!IsFinite(displayedPercentage))
            {
                return QaStatus.Warning;
            }

            return Math.Abs(displayedPercentage.Value) <= SymmetricWarningThresholdPercentage
                ? QaStatus.Acceptable
                : QaStatus.Warning;
        }

        public static QaPercentageClassification ForTargetVolumes(
            double adaptedVolume,
            double? originalVolume,
            int decimalPlaces)
        {
            if (!IsFinite(adaptedVolume) ||
                !IsFinite(originalVolume) ||
                adaptedVolume == 0 ||
                originalVolume.Value == 0)
            {
                return new QaPercentageClassification(null, QaStatus.Error);
            }

            double changePercentage =
                100.0 * (adaptedVolume - originalVolume.Value) / originalVolume.Value;
            double displayedPercentage =
                RoundPercentageForDisplay(changePercentage, decimalPlaces);

            return new QaPercentageClassification(
                displayedPercentage,
                ForSymmetricChange(displayedPercentage));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(double? value)
        {
            return value.HasValue && IsFinite(value.Value);
        }
    }
}
