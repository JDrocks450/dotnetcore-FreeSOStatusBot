using System.Threading.Tasks;
using Discord;

namespace DiscordBot.Logging
{
    public interface ILogger
    {
        void Log(string message);
        Task Log(LogMessage logMessage);
    }
}
