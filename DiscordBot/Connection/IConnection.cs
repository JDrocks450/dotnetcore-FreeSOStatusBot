using System.Threading.Tasks;

namespace DiscordBot.Connection
{
    public interface IConnection
    {
        Task Connect();        
    }
}
