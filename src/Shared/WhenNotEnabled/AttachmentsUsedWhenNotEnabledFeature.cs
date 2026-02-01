using NServiceBus.Features;

#if FileShare
using NServiceBus.Attachments.FileShare;
#elif Sql
using NServiceBus.Attachments.Sql;
#else
using NServiceBus.Attachments;
#endif

class AttachmentsUsedWhenNotEnabledFeature :
    Feature
{
#pragma warning disable CS0618 // EnableByDefault is obsolete but still needed for this feature
    public AttachmentsUsedWhenNotEnabledFeature() =>
        EnableByDefault();
#pragma warning restore CS0618

    protected override void Setup(FeatureConfigurationContext context)
    {
        if (context.Settings.TryGet<AttachmentSettings>(out _))
        {
            return;
        }

        context.Pipeline.Register(new UsedWhenNotEnabledRegistration());
    }
}