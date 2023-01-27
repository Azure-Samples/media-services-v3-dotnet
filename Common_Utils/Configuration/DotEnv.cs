// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Common_Utils
{
    /// <summary>
    /// This class is used to read the ".env" file if the IDE is Visual Studio. (VS Code has its own way to read it using launch.json)
    /// </summary>
    public static class DotEnv
    {
        public static ConfigWrapper LoadEnvOrAppSettings()
        {
            // Load the .env file in the root if it exists.
            try
            {
                DotEnv.Load(".env");
            }
            catch
            {

            }

            // Load the appsettings.json file if it exists, then finally load environment variables in deployment to override settings
            ConfigWrapper config = new(new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", true)
             .AddEnvironmentVariables()
             .Build());

            return config;
        }

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
                    2,
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
