using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public interface ICommandHandler
    {
        Task InitializeAsync();
    }
}
