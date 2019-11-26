using System;
using System.Diagnostics;
using System.IO;

namespace Switchyard.CodeGeneration
{
    public static class SvgExport
    {
        public static void GenerateSvgFromDot(string dotFilePath, string dotExeFilePath)
        {
            if (File.Exists(dotFilePath) && File.Exists(dotExeFilePath))
            {
                var process = new Process();
                var outputfile = Path.ChangeExtension(dotExeFilePath, ".svg");
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    // ReSharper disable once AssignNullToNotNullAttribute
                    FileName = dotExeFilePath,
                    Arguments = $"-Tsvg \"{dotFilePath}\" -o \"{outputfile}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    throw new Exception($"Graph generation failed with code {process.ExitCode}. Std error output: {error}");
                }
            }
        }
    }
}