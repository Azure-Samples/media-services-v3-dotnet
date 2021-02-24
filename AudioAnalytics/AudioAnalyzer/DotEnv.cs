using System;
using System.IO;

namespace AudioAnalyzer
{
    /// <summary>
    /// This class is used to read the ".env" file if the IDE is Visual Studio. (VS Code has its own way to read it using launch.json)
    /// </summary>
    public static class DotEnv
    {
        /// <summary>
        /// Loads the .env file and stores the values as variables
        /// </summary>
        /// <param name="filePath"></param>
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                if (line.StartsWith("#"))
                {
                    // It's a comment
                    continue;
                }

                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2)
                    continue;

                var p0 = parts[0].Trim();
                var p1 = parts[1].Trim();

                if (p1.StartsWith("\""))
                {
                    p1 = p1[1..^1];
                }

                Environment.SetEnvironmentVariable(p0, p1);
            }
        }
    }
}
