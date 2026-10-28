using System;
using System.Diagnostics;
using System.Windows;
using USZ_ARTEMIS.Configuration;
using USZ_ARTEMIS.Tools;

namespace USZ_ARTEMIS
{
    public partial class StartPage
    {
        private void BtnLaunchPreprocessing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(AppPaths.ConfigurationLoadError))
                    throw new InvalidOperationException(AppPaths.ConfigurationLoadError);

                var plan = GetSelectedPlan();
                var startInfo = PreprocessingLauncher.CreateStartInfo(
                    AppPaths.PreprocessingExecutablePath,
                    AppPaths.PreprocessingWorkingDirectory,
                    context.Patient?.Id, plan?.Id, plan?.UID, Environment.UserName);

                // Dispose the process handle without waiting for or terminating the GUI.
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("Windows did not start the preprocessing executable.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not launch ARTEMIS preprocessing.\n\n" + ex.Message +
                    "\n\nPath configuration: " + (AppPaths.ConfigurationSourcePath ?? "USZ_ARTEMIS.esapi.json"),
                    "ARTEMIS preprocessing launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
