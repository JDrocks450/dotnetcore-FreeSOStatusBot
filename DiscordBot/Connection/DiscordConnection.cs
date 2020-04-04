using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBot.Configuration;
using DiscordBot.Logging;

namespace DiscordBot.Connection
{
    public class DiscordConnection : IConnection
    {
        private readonly DiscordSocketClient client;
        private readonly IConfiguration config;
        private readonly ILogger logger;

        public DiscordConnection(DiscordSocketClient client, IConfiguration config, ILogger logger)
        {
            this.client = client;
            this.config = config;
            this.logger = logger;
        }

        public async Task Connect()
        {
            client.Log += logger.Log;
            await client.LoginAsync(TokenType.Bot, config.GetValueFor(Constants.ConfigKeyToken));
            await client.StartAsync();
        }
    }
}
