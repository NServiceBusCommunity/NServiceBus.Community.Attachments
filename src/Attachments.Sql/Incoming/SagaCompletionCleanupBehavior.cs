using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;

// Deletes the attachments a saga owns (via TransferToSaga) once the saga completes.
// Runs inside ReceiveBehavior so the SqlAttachmentState is available, and uses it so the delete
// commits, or rolls back, with the rest of the handler.
class SagaCompletionCleanupBehavior :
    Behavior<IInvokeHandlerContext>
{
    static ILog log = LogManager.GetLogger("AttachmentSagaCompletionCleanup");

    public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
    {
        await next();

        if (context.MessageHandler.Instance is not Saga { Completed: true, Entity: { } sagaData })
        {
            return;
        }

        if (sagaData.Id == Guid.Empty)
        {
            return;
        }

        if (!context.Extensions.TryGet<SqlAttachmentState>(out var state))
        {
            return;
        }

        var owner = SagaAttachmentOwner.Key(sagaData);
        var cancel = context.CancellationToken;
        var count = await state.Execute(
            (connection, transaction) => state.Persister.DeleteAttachments(owner, connection, transaction, cancel),
            cancel);
        if (count != 0)
        {
            log.Debug($"Deleted {count} attachments for completed saga {sagaData.Id}");
        }
    }
}
