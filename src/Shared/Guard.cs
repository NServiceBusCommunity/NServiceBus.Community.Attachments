static class Guard
{
    public static void FileExists(string path, [CallerArgumentExpression("path")] string argumentName = "")
    {
        AgainstNullOrEmpty(path, argumentName);
        if (!File.Exists(path))
        {
            throw new ArgumentException($"File does not exist: {path}", argumentName);
        }
    }

    public static void AgainstDuplicateNames(IEnumerable<string> attachmentNames)
    {
        var duplicates = attachmentNames
            .GroupBy(_ => _)
            .Where(_ => _.Count() > 1)
            .Select(_ => _.Key)
            .ToList();
        if (duplicates.Count != 0)
        {
            throw new($"Duplicate names detected: {string.Join(", ", duplicates)}");
        }
    }

    public static void AgainstEmpty(string? value, [CallerArgumentExpression("value")] string argumentName = "")
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    public static void AgainstNullOrEmpty(string value, [CallerArgumentExpression("value")] string argumentName = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    public static void AgainstLongAttachmentName(string value)
    {
        if (value.Length > 255)
        {
            throw new ArgumentException($"Attachment Name too long. Max length is 255 characters. Value: {value}");
        }
    }

    public static Func<T> WrapFuncInCheck<T>(this Func<T> func, string name) =>
        () => func.EvaluateAndCheck(name);

    static T EvaluateAndCheck<T>(this Func<T> func, string attachmentName)
    {
        var message = $"Provided delegate threw an exception. Attachment name: {attachmentName}.";
        try
        {
            return func();
        }
        catch (Exception exception)
        {
            throw new(message, exception);
        }
    }

    public static Action? WrapCleanupInCheck(this Action? cleanup, string attachmentName)
    {
        if (cleanup is null)
        {
            return null;
        }

        return () =>
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                throw new($"Cleanup threw an exception. Attachment name: {attachmentName}.", exception);
            }
        };
    }

    public static Func<Task<T>> WrapFuncTaskInCheck<T>(this Func<Task<T>> func, string attachmentName) =>
        () => func.EvaluateAndCheck(attachmentName);

    public static Func<Task<Stream>> WrapStreamFuncTaskInCheck<T>(this Func<Task<T>> func, string attachmentName)
        where T : Stream =>
        async () => await func.EvaluateAndCheck(attachmentName);

    public static Func<Stream, Task> WrapStreamWriterInCheck(this Func<Stream, Task> func, string attachmentName) =>
        async stream =>
        {
            var message = $"Provided stream writer delegate threw an exception. Attachment name: {attachmentName}.";
            try
            {
                await func(stream);
            }
            catch (Exception exception)
            {
                throw new(message, exception);
            }
        };
}