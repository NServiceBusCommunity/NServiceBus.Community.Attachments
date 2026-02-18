using System.Threading.Tasks;

public class SqlSanitizerTests
{
    [Test]
    public async Task Table_name_and_schema_should_be_quoted()
    {
        await Assert.That(SqlSanitizer.Sanitize("MyEndpoint")).IsEqualTo("[MyEndpoint]");
        await Assert.That(SqlSanitizer.Sanitize("MyEndpoint]; SOME OTHER SQL;--")).IsEqualTo("[MyEndpoint]]; SOME OTHER SQL;--]");
    }
}