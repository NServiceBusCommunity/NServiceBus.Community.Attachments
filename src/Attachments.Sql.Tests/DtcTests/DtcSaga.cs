using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NServiceBus.Persistence.Sql;

class DtcSaga(DtcTestContext context) :
    Saga<DtcSaga.SagaData>,
    IAmStartedByMessages<DtcSendMessage>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
        mapper.MapSaga(saga => saga.MyId)
            .ToMessage<DtcSendMessage>(msg => msg.MyId);

    public async Task Handle(DtcSendMessage message, HandlerContext handlerContext)
    {
        try
        {
            await HandleInner(message, handlerContext);
        }
        catch (Exception ex)
        {
            context.SagaError = ex;
            context.SagaEvent.Set();
            throw;
        }
    }

    async Task HandleInner(DtcSendMessage message, HandlerContext handlerContext)
    {
        var session = handlerContext.SynchronizedStorageSession.SqlPersistenceSession();
        var connection = (SqlConnection) session.Connection;
        var transaction = (SqlTransaction?) session.Transaction;

        var businessDb = context.BusinessDatabaseName;

        // Raw ADO.NET write using 3-part name
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"insert into [{businessDb}].[dbo].[BusinessEntities] (Id, Value) values (@Id, @Value)";
            command.Parameters.AddWithValue("@Id", Guid.NewGuid());
            command.Parameters.AddWithValue("@Value", "saga-ado-write");
            await command.ExecuteNonQueryAsync(handlerContext.CancellationToken);
        }

        context.SagaAdoWriteSucceeded = true;

        // Raw ADO.NET read
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"select count(*) from [{businessDb}].[dbo].[BusinessEntities] where Value = 'saga-ado-write'";
            var count = (int)(await command.ExecuteScalarAsync(handlerContext.CancellationToken))!;
            context.SagaAdoReadSucceeded = count > 0;
        }

        // EF Core write — uses synonym in NSB DB that points to Business DB table
        var efOptions = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseSqlServer(connection)
            .Options;

        await using (var dbContext = new BusinessDbContext(efOptions))
        {
            dbContext.Database.UseTransaction((DbTransaction?) transaction);
            dbContext.Entities.Add(new BusinessEntity
            {
                Id = Guid.NewGuid(),
                Value = "saga-ef-write"
            });
            await dbContext.SaveChangesAsync(handlerContext.CancellationToken);
        }

        context.SagaEfWriteSucceeded = true;

        // EF Core read
        await using (var dbContext = new BusinessDbContext(efOptions))
        {
            dbContext.Database.UseTransaction((DbTransaction?) transaction);
            var entity = await dbContext.Entities.FirstOrDefaultAsync(
                e => e.Value == "saga-ef-write",
                handlerContext.CancellationToken);
            context.SagaEfReadSucceeded = entity is not null;
        }

        // Read attachment
        var incomingAttachment = handlerContext.Attachments();
        await using var stream = await incomingAttachment.GetStream(handlerContext.CancellationToken);
        context.SagaAttachmentReadSucceeded = stream.Length > 0;

        context.SagaEvent.Set();
    }

    public class SagaData :
        ContainSagaData
    {
        public Guid MyId { get; set; }
    }
}
