using System;
using System.IO;

namespace AudioAnalyzer
{
    public static class DotEnv
    {
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2)
                    continue;

                var p1 = parts[1].Trim()[1..^1];

                Environment.SetEnvironmentVariable(parts[0], p1);
            }
        }
    }
}
