namespace Exceptions;


/// <summary>
/// Abstract class, derived into SlashCommandBusinessException, or SlashCommandGenericException
/// </summary>
public abstract class SlashCommandException : Exception
{
    protected SlashCommandException(string message) 
        : base(message)
    {}
}


/// <summary>
/// To be used as wrapper for expected exceptions and business logic violations
/// </summary>
public class SlashCommandBusinessException : SlashCommandException
{
    /// <summary>
    /// Message is a resource key from StringResources.Exceptions
    /// </summary>
    /// <param name="message"></param>
    public object[] Args;

    public SlashCommandBusinessException(string message, params object[] args) : base(message)
    {
        Args = args;
    }
}

/// <summary>
/// To be used as wrapper for unexpected exceptions in slash command executions
/// </summary>
public class SlashCommandGenericException : SlashCommandException
{
    public SlashCommandGenericException(string message) : base(message) { }
}