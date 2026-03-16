namespace Kitchenhelper.Core.Services;

public interface IAiChatService
{
    Task<string> GetChatResponseAsync(string userMessage);
}
