using NServiceBus.Pipeline;

class SagaCompletionCleanupRegistration :
    RegisterStep
{
    public SagaCompletionCleanupRegistration() :
        base(stepId: $"{AssemblyHelper.Name}SagaCompletionCleanup",
            behavior: typeof(SagaCompletionCleanupBehavior),
            description: "Deletes the attachments owned by a saga when it completes.",
            factoryMethod: _ => new SagaCompletionCleanupBehavior()) =>
        // needs the SqlAttachmentState that ReceiveBehavior adds to the context
        InsertAfter($"{AssemblyHelper.Name}Receive");
}
