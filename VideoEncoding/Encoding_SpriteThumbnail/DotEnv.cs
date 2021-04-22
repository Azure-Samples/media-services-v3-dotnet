// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Encoding_SpriteThumbnail
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
        public static void Load(string envFileName)
        {
            // let's find the root folder where the .env file can be found
            var rootPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase))))));
            var filePath = Path.Combine(rootPath, envFileName);
            // let's remove file://
            filePath = new Uri(filePath).LocalPath;

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
