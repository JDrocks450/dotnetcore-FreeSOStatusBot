using System;
using System.Threading.Tasks;
using Discord.WebSocket;
using DiscordBot.Connection;
using DiscordBot.Handlers;
using DiscordBot.Modules;

namespace DiscordBot
{
    public class DiscordBot
    {
        private bool canUpdateAutomatically = true;    
        private readonly IConnection connection;
        private readonly DiscordCommandHandler commandHandler;

        public DiscordBot(IConnection connection, ICommandHandler commandHandler)
        {
            this.connection = connection;
            this.commandHandler = commandHandler as DiscordCommandHandler;
        }

        public async Task Run()
        {
            await (connection as IConnection).Connect();
            await commandHandler.InitializeAsync();
            await Task.Delay(-1);
        }                 
    }
}
