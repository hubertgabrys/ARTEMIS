using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace USZ_ARTEMIS.Tools
{
    internal static class PreprocessingLauncher
    {
        internal static ProcessStartInfo CreateStartInfo(
            string executablePath, string workingDirectory,
            string patientId, string planId, string planUid, string username)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                throw new InvalidOperationException("Please select a patient in Eclipse before launching preprocessing.");
            if (string.IsNullOrWhiteSpace(planId) || string.IsNullOrWhiteSpace(planUid))
                throw new InvalidOperationException("Please open a plan in Eclipse before launching preprocessing.");
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("The Windows username is unavailable.");

            if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(workingDirectory))
                throw new InvalidOperationException(
                    "Set PreprocessingExecutablePath and PreprocessingWorkingDirectory in the ARTEMIS path configuration.");
            if (!IsAbsolutePath(executablePath) || !IsAbsolutePath(workingDirectory))
                throw new InvalidOperationException("Preprocessing paths must be absolute paths.");
            if (!File.Exists(executablePath))
                throw new FileNotFoundException("Preprocessing executable not found or inaccessible:\n" + executablePath);
            if (!Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException("Preprocessing working directory not found or inaccessible:\n" + workingDirectory);

            return new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                // Keep the standalone launcher's four positional arguments.
                Arguments = string.Join(" ", new[]
                {
                    QuoteArgument(patientId.Trim()), QuoteArgument(planId.Trim()),
                    QuoteArgument(planUid.Trim()), QuoteArgument(username.Trim())
                }),
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static bool IsAbsolutePath(string path)
        {
            // A rooted Windows path such as C:tool.exe or \tool.exe still depends
            // on Eclipse's current directory or drive.
            return Path.IsPathRooted(path) && string.Equals(
                Path.GetPathRoot(path), Path.GetPathRoot(Path.GetFullPath(path)),
                StringComparison.OrdinalIgnoreCase);
        }

        // .NET Framework lacks ArgumentList. Escape quotes and trailing backslashes
        // according to the Windows command-line rules used by the Python executable.
        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                result.Append(character);
                backslashes = 0;
            }

            result.Append('\\', backslashes * 2);
            return result.Append('"').ToString();
        }
    }
}
