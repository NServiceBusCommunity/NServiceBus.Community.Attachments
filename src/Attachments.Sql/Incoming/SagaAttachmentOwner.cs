using NServiceBus;

static class SagaAttachmentOwner
{
    // stored in the MessageId column, so must fit in nvarchar(50). "saga-" plus a 36 char guid is 41
    public static string Key(IContainSagaData saga)
    {
        if (saga.Id == Guid.Empty)
        {
            throw new ArgumentException("Saga Id is empty. Attachments can only be transferred to a saga once its Id has been assigned.", nameof(saga));
        }

        return $"saga-{saga.Id}";
    }

    public static DateTime? Expiry(TimeSpan? timeToKeep)
    {
        if (timeToKeep is null)
        {
            return null;
        }

        return DateTime.UtcNow.Add(timeToKeep.Value);
    }
}
