public class Usage
{
    Usage(EndpointConfiguration configuration)
    {
        #region FileShareEnableAttachments

        configuration.EnableAttachments(
            fileShare: "networkSharePath",
            timeToKeep: _ => TimeSpan.FromDays(7));

        #endregion

        #region FileShareEnableAttachmentsRecommended

        configuration.EnableAttachments(
            fileShare: "networkSharePath",
            timeToKeep: TimeToKeep.Default);

        #endregion
    }

    static void DisableCleanupTask(EndpointConfiguration configuration)
    {
        #region FileShareDisableCleanupTask

        var attachments = configuration.EnableAttachments(
            fileShare: "networkSharePath",
            timeToKeep: TimeToKeep.Default);
        attachments.DisableCleanupTask();

        #endregion
    }
}
