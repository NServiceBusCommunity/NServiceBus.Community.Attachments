using NServiceBus.Attachments.Sql;
using NServiceBus.Installation;
using NServiceBus.Settings;
using Installer = NServiceBus.Attachments.Sql.Installer;

class NeedToInstallSomething :
    INeedToInstallSomething
{
    AttachmentSettings? settings;

    public NeedToInstallSomething(IReadOnlySettings settings) =>
        this.settings = settings.GetOrDefault<AttachmentSettings?>();

    public async Task Install(string identity, Cancel cancel = default)
    {
        if (settings == null || settings.InstallerDisabled)
        {
            return;
        }

        await using var connection = await settings.ConnectionFactory(cancel);
        await Installer.CreateTable(connection, settings.Database, settings.Schema, settings.TableName, cancel);
    }
}