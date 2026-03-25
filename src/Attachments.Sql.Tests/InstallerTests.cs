public class InstallerTests
{
    [Test]
    public async Task Run()
    {
        await using var database = await Connection.SqlInstance.Build();
        var connection = database.Connection;
        await TableExists("[dbo].[MessageAttachments]", connection);
    }

    static async Task TableExists(string tableName, SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             select case when exists(
                 select * from sys.objects where
                     object_id = object_id('{tableName}')
                     and type in ('U')
             ) then 1 else 0 end;
             """;
        var tableExists = (int) command.ExecuteScalar()! == 1;
        await Assert.That(tableExists).IsTrue();
    }
}
