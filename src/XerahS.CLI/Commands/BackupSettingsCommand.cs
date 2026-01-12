using System.CommandLine;
using System.CommandLine.Invocation;
using XerahS.CLI;
using XerahS.Core;

namespace XerahS.CLI.Commands
{
    public class BackupSettingsCommand : Command
    {
        public BackupSettingsCommand() : base("backup-settings", "Force a backup of application settings")
        {
            this.SetAction((parseResult) => Execute());
        }

        public static Command Create()
        {
            return new BackupSettingsCommand();
        }

        private void Execute()
        {
            try
            {
                Console.WriteLine("Loading settings...");
                SettingsManager.LoadInitialSettings();

                Console.WriteLine("Backing up settings...");
                SettingsManager.SaveAllSettings();

                Console.WriteLine($"[SUCCESS] Settings backed up to: {SettingsManager.BackupFolder}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Failed to backup settings: {ex.Message}");
            }
        }
    }
}
