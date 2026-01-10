using System.CommandLine;
using XerahS.Core;

namespace XerahS.CLI.Commands
{
    public class BackupSettingsCommand : Command
    {
        public BackupSettingsCommand() : base("backup-settings", "Force a backup of application settings")
        {
            this.SetHandler(Execute);
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
                SettingManager.LoadInitialSettings();

                Console.WriteLine("Backing up settings...");
                SettingManager.SaveAllSettings();

                Console.WriteLine($"[SUCCESS] Settings backed up to: {SettingManager.BackupFolder}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Failed to backup settings: {ex.Message}");
            }
        }
    }
}
