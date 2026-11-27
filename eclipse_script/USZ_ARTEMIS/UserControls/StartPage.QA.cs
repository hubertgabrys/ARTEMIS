using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using USZ_ARTEMIS.Configuration;
using USZ_ARTEMIS.Core.QA;
using USZ_ARTEMIS.QA;
using USZ_ARTEMIS.Tools;
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

        private void CbMachine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateQaSendLabel();
        }

        private void UpdateQaSendLabel()
        {
            // SelectionChanged can fire before the QA buttons are created by XAML.
            if (btnSendPlan != null)
                btnSendPlan.Content = DoseCheckLauncher.IsAccelaMachine(GetSelectedMachineId())
                    ? "Send to DoseCHECK"
                    : "Send to SciMoCa";
        }

        private async void BtnPerformQA_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetQaPlan(out PlanSetup plan, out bool useDoseCheck))
                return;

            if (useDoseCheck)
            {
                await RunDoseCheckAsync(plan, waterPsqa: true);
                return;
            }

            PerformEclipseWaterQa();
        }

        private void PerformEclipseWaterQa()
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

        private async void BtnSendPlan_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetQaPlan(out PlanSetup plan, out bool useDoseCheck))
                return;

            if (useDoseCheck)
            {
                await RunDoseCheckAsync(plan, waterPsqa: false);
                return;
            }

            try
            {
                if (!plan.IsDoseValid || string.IsNullOrWhiteSpace(plan.Dose?.UID))
                    throw new InvalidOperationException("Calculate the dose of the open plan before sending it.");

                Scimoca.SendToScimoca(context.Patient.Id, plan.Id, plan.UID, plan.Dose.UID, Environment.UserName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Send to SciMoCa", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetQaPlan(out PlanSetup plan, out bool useDoseCheck)
        {
            plan = GetSelectedPlan();
            useDoseCheck = false;
            try
            {
                if (plan == null || context.Patient == null)
                    throw new InvalidOperationException("Open the treatment plan in Eclipse before running QA.");

                useDoseCheck = DoseCheckLauncher.UseDoseCheck(GetSelectedMachineId(), plan.Beams
                    .Where(beam => !beam.IsSetupField)
                    .Select(beam => beam.TreatmentUnit?.Id));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Treatment Machine", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private async Task RunDoseCheckAsync(PlanSetup plan, bool waterPsqa)
        {
            string title = waterPsqa ? "DoseCHECK water PSQA" : "Send to DoseCHECK";
            string executable = waterPsqa ? AppPaths.DoseCheckPsqaExecutablePath : AppPaths.DoseCheckExecutablePath;
            string workingDirectory = waterPsqa ? AppPaths.DoseCheckPsqaWorkingDirectory : AppPaths.DoseCheckWorkingDirectory;
            btnSendPlan.IsEnabled = false;
            btnPerformQA.IsEnabled = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(AppPaths.ConfigurationLoadError))
                    throw new InvalidOperationException(AppPaths.ConfigurationLoadError);
                if (!plan.IsDoseValid)
                    throw new InvalidOperationException("Calculate and save the dose of the open plan before exporting it.");

                var startInfo = DoseCheckLauncher.CreateStartInfo(executable, workingDirectory,
                    context.Patient.Id, plan.Id, plan.UID, plan.Dose?.UID, Environment.UserName, waterPsqa);
                int exitCode = await ExternalProcessLauncher.RunAsync(startInfo);
                if (exitCode != 0)
                    throw new InvalidOperationException(
                        $"The exporter exited with code {exitCode}. Verify that the plan and dose were saved in Eclipse.\n" +
                        $"Review the exporter log in {Path.Combine(Path.GetDirectoryName(executable), "logs")} " +
                        "and check DoseCHECK for partial delivery before retrying.");

                MessageBox.Show(
                    "The DoseCHECK DICOM listener accepted the export. Check DoseCHECK for the result.",
                    title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSendPlan.IsEnabled = true;
                btnPerformQA.IsEnabled = true;
            }
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
