using System;
using System.IO;
using NUnit.Framework;

namespace TalosAI.Tests
{
    public static class TestRunSettings
    {
        public static string ResultsDirectory => Path.Combine(AppContext.BaseDirectory, "TestResults");
        public static string ResultsJson => Path.Combine(ResultsDirectory, "TestRun.json");
    }
}
