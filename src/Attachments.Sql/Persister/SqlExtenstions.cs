using System.Data;
using Microsoft.Data.SqlClient;

static class SqlExtensions
{
    public static string? GetStringOrNull(this IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }

        return reader.GetString(index);
    }

    public static void AddParameter(this SqlCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.DbType = DbType.String;
        parameter.ParameterName = name;
        if (value is null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            parameter.Value = value;
        }

        command.Parameters.Add(parameter);
    }

    public static void AddBinary(this SqlCommand command, string name, Stream value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Binary;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public static string GetString(this SqlDataReader reader, int column, Encoding encoding)
    {
        using var stream = reader.GetStream(column);
        using var streamReader = new StreamReader(stream, encoding, true);
        return streamReader.ReadToEnd();
    }

    public static void AddParameter(this SqlCommand command, string name, DateTime value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.DateTime2;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}