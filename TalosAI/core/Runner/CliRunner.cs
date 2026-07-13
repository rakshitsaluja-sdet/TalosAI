using System;
using System.Diagnostics;

namespace TalosAI.Tests
{
    public static class CliRunner
    {
        public static int RunByTag(string tag)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test --filter TestCategory={tag} --logger trx;LogFileName=TestResults.trx",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return -1;
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        }
    }
}
