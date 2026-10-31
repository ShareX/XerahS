#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.CommandLine;
using XerahS.Bootstrap;
using XerahS.CLI.Commands;
using XerahS.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using XerahS.Common;
using XerahS.Assistant.Services;

namespace XerahS.CLI
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Existing-file inspection needs neither a desktop session nor saved configuration.
                // Avoid bootstrap logging so stdout is a machine-readable JSON report.
                if (args.FirstOrDefault() == "verify-recording" &&
                    args.Any(arg => arg == "--input" || arg.StartsWith("--input=", StringComparison.Ordinal)))
                    return await VerifyRecordingCommand.Create(null).Parse(args.Skip(1).ToArray()).InvokeAsync();

                if (CloudCommand.TryForwardCallback(args, out int callbackExitCode))
                {
                    return callbackExitCode;
                }

                bool isAssistantMode = TryGetAssistantPrompt(args, out _);
                bool needsRecording = CommandNeedsRecording(args);

                // Initialize in headless mode
                var bootstrapOptions = new BootstrapOptions
                {
                    EnableLogging = !isAssistantMode,
                    InitializeRecording = needsRecording,
                    UIService = new HeadlessUIService(),
                    ToastService = new HeadlessToastService()
                };

                var result = await ShareXBootstrap.InitializeAsync(bootstrapOptions);

                if (!result.PlatformServicesInitialized)
                {
                    Console.Error.WriteLine("Failed to initialize platform services");
                    return 1;
                }

                if (!result.ConfigurationLoaded)
                {
                    Console.Error.WriteLine("Failed to load configuration");
                    return 1;
                }

                IServiceProvider services = result.ServiceProvider
                    ?? throw new InvalidOperationException("Bootstrap completed without a service provider.");

                var taskManager = services.GetRequiredService<IDesktopTaskManager>();
                var recordingCoordinator = services.GetRequiredService<IScreenRecordingCoordinator>();
                var assistantCliService = new AssistantCliService(new AssistantService(taskManager));

                // Build command tree
                var rootCommand = BuildRootCommand(services, taskManager, recordingCoordinator, assistantCliService);

                // Execute
                return await rootCommand.Parse(args).InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static bool CommandNeedsRecording(string[] args)
        {
            string? command = args.FirstOrDefault(arg => !arg.StartsWith("-", StringComparison.Ordinal));
            return command is "record" or "verify-recording" or "verify-gif-recording";
        }

        private static bool TryGetAssistantPrompt(string[] args, out string? prompt)
        {
            prompt = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--assistant", StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    return false;
                }

                prompt = args[i + 1];
                return !string.IsNullOrWhiteSpace(prompt);
            }

            return false;
        }

        private static RootCommand BuildRootCommand(
            IServiceProvider services,
            IDesktopTaskManager taskManager,
            IScreenRecordingCoordinator recordingCoordinator,
            AssistantCliService assistantCliService)
        {
            var rootCommand = new RootCommand("XerahS CLI - ShareX workflow automation");
            var assistantOption = new Option<string?>("--assistant")
            {
                Description = "Run the intelligent assistant prompt in headless mode."
            };

            rootCommand.Add(assistantOption);
            rootCommand.Add(WorkflowCommand.Create(taskManager, recordingCoordinator));
            rootCommand.Add(RecordCommand.Create(recordingCoordinator));
            rootCommand.Add(CaptureCommand.Create(taskManager));
            rootCommand.Add(ListCommand.Create());
            rootCommand.Add(ConfigCommand.Create());
            rootCommand.Add(BackupSettingsCommand.Create());
            rootCommand.Add(RestoreSettingsCommand.Create());
            rootCommand.Add(VerifyRegionCaptureCommand.Create());
            rootCommand.Add(VerifyMacOSNativeCrosshairCommand.Create(taskManager));
            rootCommand.Add(CompareCaptureCommand.Create());
            rootCommand.Add(VerifyRecordingCommand.Create(recordingCoordinator));
            rootCommand.Add(VerifyGifRecordingCommand.Create(recordingCoordinator));
            rootCommand.Add(VerifyVideoEditorCommand.Create());
            rootCommand.Add(OpenVideoEditorCommand.Create());
            rootCommand.Add(WatchFolderDaemonCommand.Create());
            rootCommand.Add(UploadCommand.Create(taskManager));
            rootCommand.Add(IndexCommand.Create());
            rootCommand.Add(ReClipCommand.Create());
            rootCommand.Add(DoctorCommand.Create());
            rootCommand.Add(BootstrapCommand.Create());
            rootCommand.Add(OpenClawCommand.Create());
            rootCommand.Add(CloudCommand.Create(services));

            rootCommand.SetAction(async parseResult =>
            {
                string? prompt = parseResult.GetValue(assistantOption);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    Console.Error.WriteLine("No command specified. Use --help to see available commands.");
                    Environment.ExitCode = 1;
                    return;
                }

                Environment.ExitCode = await assistantCliService.RunAsync(prompt, CancellationToken.None);
            });

            return rootCommand;
        }
    }
}
