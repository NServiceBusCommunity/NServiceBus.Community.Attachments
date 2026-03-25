using NServiceBus.Persistence.Sql;

class DtcSendHandler(DtcTestContext context) :
    IHandleMessages<DtcSendMessage>
{
    public async Task Handle(DtcSendMessage message, HandlerContext handlerContext)
    {
        try
        {
            await HandleInner(message, handlerContext);
        }
        catch (Exception ex)
        {
            context.HandlerError = ex;
            context.HandlerEvent.Set();
            throw;
        }
    }

    async Task HandleInner(DtcSendMessage message, HandlerContext handlerContext)
    {
        var session = handlerContext.SynchronizedStorageSession.SqlPersistenceSession();
        var connection = (SqlConnection) session.Connection;
        var transaction = (SqlTransaction?) session.Transaction;

        var businessDb = context.BusinessDatabaseName;

        // Raw ADO.NET write using 3-part name through persistence connection
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"insert into [{businessDb}].[dbo].[BusinessEntities] (Id, Value) values (@Id, @Value)";
            command.Parameters.AddWithValue("@Id", Guid.NewGuid());
            command.Parameters.AddWithValue("@Value", "handler-ado-write");
            await command.ExecuteNonQueryAsync(handlerContext.CancellationToken);
        }

        context.HandlerAdoWriteSucceeded = true;

        // Raw ADO.NET read using 3-part name
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"select count(*) from [{businessDb}].[dbo].[BusinessEntities]";
            var count = (int)(await command.ExecuteScalarAsync(handlerContext.CancellationToken))!;
            context.HandlerAdoReadSucceeded = count > 0;
        }

        // EF Core write — uses synonym in NSB DB that points to Business DB table
        var efOptions = CreateEfOptions(connection);

        await using (var dbContext = new BusinessDbContext(efOptions))
        {
            dbContext.Database.UseTransaction((DbTransaction?) transaction);
            dbContext.Entities.Add(new BusinessEntity
            {
                Id = Guid.NewGuid(),
                Value = "handler-ef-write"
            });
            await dbContext.SaveChangesAsync(handlerContext.CancellationToken);
        }

        context.HandlerEfWriteSucceeded = true;

        // EF Core read
        await using (var dbContext = new BusinessDbContext(efOptions))
        {
            dbContext.Database.UseTransaction((DbTransaction?) transaction);
            var entity = await dbContext.Entities.FirstOrDefaultAsync(
                e => e.Value == "handler-ef-write",
                handlerContext.CancellationToken);
            context.HandlerEfReadSucceeded = entity is not null;
        }

        // Read attachment
        var incomingAttachments = handlerContext.Attachments();
        var attachment = await incomingAttachments.GetBytes("withMetadata", handlerContext.CancellationToken);
        context.HandlerAttachmentReadSucceeded = attachment.Metadata["key"] == "value";

        // Send reply with attachment
        var replyOptions = new SendOptions();
        replyOptions.RouteToThisEndpoint();
        var outgoing = replyOptions.Attachments();
        outgoing.AddBytes(attachment);
        await handlerContext.Send(new DtcReplyMessage(), replyOptions);
    }

    static DbContextOptions<BusinessDbContext> CreateEfOptions(SqlConnection connection) =>
        new DbContextOptionsBuilder<BusinessDbContext>()
            .UseSqlServer(connection)
            .Options;
}
