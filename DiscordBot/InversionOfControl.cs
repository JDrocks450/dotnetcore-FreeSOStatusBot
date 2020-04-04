using Discord.WebSocket;
using DiscordBot.Configuration;
using DiscordBot.Connection;
using DiscordBot.Handlers;
using DiscordBot.Logging;
using Lamar;

namespace DiscordBot
{
    public static class InversionOfControl
    {
        private static Container container;

        public static Container Container
        {
            get
            {
                return GetOrInitContainer();
            }
        }

        private static Container GetOrInitContainer()
        {
            if(container is null)
            {
                InitializeContainer();
            }

            return container;
        }

        public static void InitializeContainer()
        {
            container = new Container(c =>
            {
                c.For<IConnection>().Use<DiscordConnection>();
                c.For<IConfiguration>().Use<ConfigManager>();
                c.For<ICommandHandler>().Use<DiscordCommandHandler>();
                c.For<ILogger>().Use<ConsoleLogger>();
                c.ForSingletonOf<DiscordSocketClient>().UseIfNone(DiscordSocketClientFactory.GetDefault());
                // Add the types you need with:
                // c.For<YourInterface>().Use<YourConcretion>();
                // c.ForSingletonOf<YourSingletonType>().UseIfNone<YourSingletonType>();
            });
        }
    }
}
