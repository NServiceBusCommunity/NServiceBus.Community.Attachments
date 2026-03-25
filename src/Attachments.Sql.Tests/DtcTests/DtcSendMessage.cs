class DtcSendMessage : IMessage
{
    public Guid MyId { get; set; } = Guid.NewGuid();
}
