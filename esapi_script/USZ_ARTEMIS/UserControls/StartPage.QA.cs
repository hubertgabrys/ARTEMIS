using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using USZ_ARTEMIS.Configuration;
using USZ_ARTEMIS.Core.QA;
using USZ_ARTEMIS.QA;
using VMS.TPS.Common.Model.API;

namespace USZ_ARTEMIS
{
    public partial class StartPage
    {
        private const string QaOkPrefix = "\u2713 ";
        private const string QaWarningPrefix = "\u26A0 ";
        private const string QaErrorPrefix = "\u2717 ";

        private void BtnComparePlans_Click(object sender, RoutedEventArgs e)
        {
            PlanSetup selectedPlan = GetSelectedPlan();
            PlanSetup originalPlan = QA.Tools.GetOriginalPlan(selectedPlan);

            FlowDocument outputQA = new FlowDocument
            {
                PagePadding = new Thickness(4)
            };

            bool isDosePerFractionIdentical =
                selectedPlan.DosePerFraction.ValueAsString.Equals(originalPlan.DosePerFraction.ValueAsString);
            if (isDosePerFractionIdentical)
            {
                AddQaStatusLine(
                    outputQA,
                    "Dose/fx is identical",
                    QaStatusClassifier.ForMatch(isDosePerFractionIdentical));
            }
            else
            {
                AddQaStatusLine(
                    outputQA,
                    "Dose/fx is different!",
                    QaStatusClassifier.ForMatch(isDosePerFractionIdentical));
            }
            AddQaOutputSeparator(outputQA);

            bool isReferencePointIdentical =
                selectedPlan.PrimaryReferencePoint.Id.Equals(originalPlan.PrimaryReferencePoint.Id);
            if (isReferencePointIdentical)
            {
                AddQaStatusLine(
                    outputQA,
                    "Ref. point is identical",
                    QaStatusClassifier.ForMatch(isReferencePointIdentical));
            }
            else
            {
                AddQaStatusLine(
                    outputQA,
                    "Ref. point is different!",
                    QaStatusClassifier.ForMatch(isReferencePointIdentical));
            }
            AddQaOutputSeparator(outputQA);

            List<Beam> inBeamsSelected = selectedPlan.Beams.ToList();
            List<Beam> therapyBeamsSelected = new List<Beam>();
            foreach (Beam beam in inBeamsSelected)
            {
                if (!beam.IsSetupField)
                {
                    therapyBeamsSelected.Add(beam);
                }
            }

            int numberOfTherapyBeamsSelected = therapyBeamsSelected.Count;

            List<double> adaptedMmos = new List<double>();
            int counterSelected = 0;
            foreach (double mmo in MetricsCalc.MMO(selectedPlan.Beams))
            {
                if (counterSelected != 0 && counterSelected <= numberOfTherapyBeamsSelected)
                {
                    adaptedMmos.Add(mmo);
                }
                counterSelected++;
            }

            List<double> originalMmos = new List<double>();
            int counterOriginal = 0;
            foreach (double mmo in MetricsCalc.MMO(originalPlan.Beams))
            {
                if (counterOriginal != 0 && counterOriginal <= numberOfTherapyBeamsSelected)
                {
                    originalMmos.Add(mmo);
                }
                counterOriginal++;
            }

            int numberOfMmoPairs = Math.Min(adaptedMmos.Count, originalMmos.Count);
            for (int i = 0; i < numberOfMmoPairs; i++)
            {
                double adaptedMmo = adaptedMmos[i];
                double originalMmo = originalMmos[i];

                if (Math.Abs(originalMmo) < 1e-9)
                {
                    AddQaStatusLine(
                        outputQA,
                        "MMO = " + adaptedMmo.ToString("F1") +
                        " mm (reference " + originalMmo.ToString("F1") +
                        " mm; n/a)",
                        QaStatusClassifier.ForMmoChange(null));
                }
                else
                {
                    double changePerc = 100.0 * (adaptedMmo - originalMmo) / originalMmo;
                    double displayedChangePerc =
                        QaStatusClassifier.RoundPercentageForDisplay(changePerc, 1);
                    string sign = displayedChangePerc >= 0 ? "+" : "";

                    AddQaStatusLine(
                        outputQA,
                        "MMO = " + adaptedMmo.ToString("F1") +
                        " mm (reference " + originalMmo.ToString("F1") +
                        " mm; " + sign + displayedChangePerc.ToString("F1") + "%)",
                        QaStatusClassifier.ForMmoChange(displayedChangePerc));
                }
            }
            AddQaOutputSeparator(outputQA);

            foreach (TargetVolumeComparison comparison in QA.Tools.GetAllTargetVolumeComparisons(selectedPlan, originalPlan))
            {
                AddQaStatusLine(
                    outputQA,
                    comparison.Text,
                    comparison.Status);
            }
            AddQaOutputSeparator(outputQA);

            double totalMuSelected = 0;
            double totalMuOriginal = 0;
            for (int iBeam = 0; iBeam < numberOfTherapyBeamsSelected; iBeam++)
            {
                totalMuSelected += therapyBeamsSelected[iBeam].Meterset.Value;
            }

            List<Beam> inBeamsOriginal = originalPlan.Beams.ToList();
            List<Beam> therapyBeamsOriginal = new List<Beam>();
            foreach (Beam beam in inBeamsOriginal)
            {
                if (!beam.IsSetupField)
                {
                    therapyBeamsOriginal.Add(beam);
                }
            }

            int numberOfTherapyBeamsOriginal = therapyBeamsOriginal.Count;
            for (int iBeam = 0; iBeam < numberOfTherapyBeamsOriginal; iBeam++)
            {
                totalMuOriginal += therapyBeamsOriginal[iBeam].Meterset.Value;
            }

            double muChangePerc = 100 * (totalMuSelected - totalMuOriginal) / totalMuOriginal;
            double displayedMuChangePerc =
                QaStatusClassifier.RoundPercentageForDisplay(muChangePerc, 1);
            AddQaStatusLine(
                outputQA,
                "MUs Change = " + displayedMuChangePerc.ToString("F1") + " %",
                QaStatusClassifier.ForSymmetricChange(displayedMuChangePerc));
            AddQaOutputSeparator(outputQA);

            txtOutputQA.Document = outputQA;
        }

        private static void AddQaStatusLine(FlowDocument document, string text, QaStatus status)
        {
            switch (status)
            {
                case QaStatus.Acceptable:
                    AddQaOutputLine(document, QaOkPrefix + text, Brushes.Green);
                    break;
                case QaStatus.Warning:
                    AddQaOutputLine(document, QaWarningPrefix + text, Brushes.DarkOrange);
                    break;
                case QaStatus.Error:
                    AddQaOutputLine(document, QaErrorPrefix + text, Brushes.Red);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        private static void AddQaOutputLine(FlowDocument document, string text, Brush foreground = null)
        {
            Run run = new Run(text);
            if (foreground != null)
            {
                run.Foreground = foreground;
            }

            document.Blocks.Add(new Paragraph(run)
            {
                Margin = new Thickness(0)
            });
        }

        private static void AddQaOutputSeparator(FlowDocument document)
        {
            AddQaOutputLine(document, "________________________________");
            document.Blocks.Add(new Paragraph
            {
                Margin = new Thickness(0),
                FontSize = 6
            });
        }

        private void BtnPerformQA_Click(object sender, RoutedEventArgs e)
        {
            Course qaCourse = Tools.Actions.CreateQaCourse(context, GetSelectedCourse());
            qaCourse.CopyPlanSetup(GetSelectedPlan());
            StructureSet qaStructureSetWater = Tools.Actions.CreateQaStructureSet(context, GetSelectedPlan(), "QW");
            PlanSetup qaPlanWater = Tools.Actions.CreateQaPlan(context, GetSelectedCourse(), GetSelectedPlan(), "QW");

            Tools.Actions.OverrideBody(
                context,
                GetSelectedCourse(),
                GetSelectedPlan(),
                "QW",
                qaStructureSetWater.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL"));

            Tools.Actions.RecalculatePlan(context, GetSelectedPlan(), qaPlanWater);
            Tools.Actions.RenameCopyPlan(context, GetSelectedPlan(), GetSelectedCourse());
            SyntheticCT.CreatePDF(context, GetSelectedPlan(), qaPlanWater);
        }

        private void BtnSendToSciMoCa_Click(object sender, RoutedEventArgs e)
        {
            string patientId = context.Patient.Id;
            string planSetupId = context.PlanSetup.Id;
            string planSetupUID = context.PlanSetup.UID;
            string planDoseUID = context.PlanSetup.Dose.UID;
            string username = Environment.UserName;

            Scimoca.SendToScimoca(patientId, planSetupId, planSetupUID, planDoseUID, username);
        }

        private void BtnPerformRegCheck_Click(object sender, RoutedEventArgs e)
        {
            string exePath = AppPaths.RegistrationCheckExecutablePath;

            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Executable not found:\n{exePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start:\n{exePath}\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
