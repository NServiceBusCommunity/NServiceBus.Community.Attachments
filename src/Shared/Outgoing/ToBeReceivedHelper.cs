using NServiceBus.Extensibility;
using NServiceBus.Performance.TimeToBeReceived;

static class ToBeReceivedHelper
{
    public static TimeSpan? GetTimeToBeReceivedFromConstraint(this ContextBag extensions)
    {
        if (extensions.TryGet(out DiscardIfNotReceivedBefore? constraint))
        {
            return constraint.MaxTime;
        }

        return null;
    }
}