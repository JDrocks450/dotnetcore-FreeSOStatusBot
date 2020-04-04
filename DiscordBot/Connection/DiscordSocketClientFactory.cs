using Discord;
using Discord.WebSocket;

namespace DiscordBot.Connection
{
    public static class DiscordSocketClientFactory
    {
        public static DiscordSocketClient GetDefault()
        {
            return new DiscordSocketClient(new DiscordSocketConfig{
                LogLevel = LogSeverity.Verbose
            });
        }
    }
}
