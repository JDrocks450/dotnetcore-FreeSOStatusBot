using System;
using System.Threading.Tasks;
using Discord;

namespace DiscordBot.Logging
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public Task Log(LogMessage logMessage)
        {
            Log(logMessage.Message);
            return Task.CompletedTask;
        }
    }
}
