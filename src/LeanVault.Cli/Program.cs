using System;
using System.CommandLine;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LeanVault.Cli
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("LeanVault CAD-aware CLI wrapper for Plastic SCM");

            var jsonOption = new Option<bool>("--json", "Output machine-readable JSON");

            // lv status
            var statusCommand = new Command("status", "Show status of the workspace formatted for CAD context");
            statusCommand.AddOption(jsonOption);
            statusCommand.SetHandler((json) => ExecuteCm("status", json), jsonOption);
            rootCommand.Add(statusCommand);

            // lv checkout <file>
            var fileArg = new Argument<string>("file", "The file path");
            var checkoutCommand = new Command("checkout", "Check out a file");
            checkoutCommand.AddArgument(fileArg);
            checkoutCommand.AddOption(jsonOption);
            checkoutCommand.SetHandler((file, json) => ExecuteCm($"checkout \"{file}\"", json), fileArg, jsonOption);
            rootCommand.Add(checkoutCommand);

            // lv checkin <file> -m "message"
            var messageOption = new Option<string>(new[] { "--message", "-m" }, "Commit message") { IsRequired = true };
            var checkinCommand = new Command("checkin", "Check in a file with SW property extraction");
            checkinCommand.AddArgument(fileArg);
            checkinCommand.AddOption(messageOption);
            checkinCommand.AddOption(jsonOption);
            checkinCommand.SetHandler((file, msg, json) => ExecuteCm($"checkin \"{file}\" -m \"{msg}\"", json), fileArg, messageOption, jsonOption);
            rootCommand.Add(checkinCommand);

            // lv checkin-assembly <sldasm>
            var assyArg = new Argument<string>("sldasm", "The assembly file path");
            var checkinAssyCommand = new Command("checkin-assembly", "Walk SW references, check in all modified (NOTE: Requires SolidWorks named-pipe server - currently a stub)");
            checkinAssyCommand.AddArgument(assyArg);
            checkinAssyCommand.AddOption(jsonOption);
            checkinAssyCommand.SetHandler((file, json) => 
            {
                // In a real implementation this would connect to the running SolidWorks instance via COM,
                // walk the tree, and invoke cm checkin for multiple files.
                Console.WriteLine(json ? "{\"status\":\"Checkin assembly initiated (stub)\"}" : $"Checking in assembly {file}...");
            }, assyArg, jsonOption);
            rootCommand.Add(checkinAssyCommand);

            // lv bom <sldasm>
            var bomCommand = new Command("bom", "Extract and print BOM as JSON (NOTE: Requires SolidWorks named-pipe server - currently a stub)");
            bomCommand.AddArgument(assyArg);
            bomCommand.AddOption(jsonOption);
            bomCommand.SetHandler((file, json) => 
            {
                // Connect to SolidWorks, read properties, return JSON
                Console.WriteLine(json ? "{\"bom\":[]}" : $"Extracting BOM for {file}...");
            }, assyArg, jsonOption);
            rootCommand.Add(bomCommand);

            // lv history <file>
            var historyCommand = new Command("history", "Log history filtered for CAD metadata");
            historyCommand.AddArgument(fileArg);
            historyCommand.AddOption(jsonOption);
            historyCommand.SetHandler((file, json) => ExecuteCm($"log \"{file}\"", json), fileArg, jsonOption);
            rootCommand.Add(historyCommand);

            return await rootCommand.InvokeAsync(args);
        }

        private static void ExecuteCm(string args, bool isJson)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cm",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return;
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (isJson)
                {
                    Console.WriteLine($"{{\"output\":\"{Escape(output)}\", \"error\":\"{Escape(error)}\", \"exitCode\":{process.ExitCode}}}");
                }
                else
                {
                    if (!string.IsNullOrEmpty(output)) Console.WriteLine(output);
                    if (!string.IsNullOrEmpty(error)) Console.Error.WriteLine(error);
                }
            }
            catch (Exception ex)
            {
                if (isJson) Console.WriteLine($"{{\"error\":\"{Escape(ex.Message)}\"}}");
                else Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }

        private static string Escape(string input) => input.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}
