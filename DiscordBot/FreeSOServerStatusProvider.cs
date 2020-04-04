using Discord;
using Discord.WebSocket;
using DiscordBot.Configuration;
using DiscordBot.Modules;
using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot
{
    public enum ServerStatus
    {
        ONLINE,
        OFFLINE
    }
    public class FreeSOStatusProvider
    {
        private bool canUpdateAutomatically = true;
        private DiscordSocketClient Client;
        private static FreeSOStatusProvider instance;
        private Thread autoUpdateThread;
        private ServerStatus lastServerStatus;
        private bool maintainanceNotificationSent = false;
        private Dictionary<ulong, ulong[]> statusUpdateChannels = new Dictionary<ulong, ulong[]>();

        public bool SilentMode
        {
            get; set;
        }        

        private string CityStatusMessage
        {
            get
            {
                var freeSOStatus = FreeSOApi.GetFreeSOStatus().Result;
                return "🌄City Status: " + ((freeSOStatus.IsOnline) ? "✅" : (freeSOStatus.IsMaintainanceActive) ? "🔥" : "⛔");
            }
        }
        private string PlayerStatusMessage
        {
            get
            {
                var freeSOStatus = FreeSOApi.GetFreeSOStatus().Result;
                return $"🌐 🏡: {freeSOStatus.LotsOnline} 👥: {freeSOStatus.PlayersOnline}";
            }
        }

        public bool IsAutoUpdating { get; internal set; }

        public static FreeSOStatusProvider Get()
        {
            return instance;
        }

        public FreeSOStatusProvider(DiscordSocketClient client)
        {
            instance = this;
            Client = client;
            Init();
        }

        private void Init()
        {
            bool canUpdate = false;
            try
            {
                canUpdate = bool.Parse(Configuration.ConfigManager.GetValue(Constants.AutoSetupAllowed));
            }
            catch(Exception e)
            {
                Console.WriteLine("Config Error! AutoSetupAllowed is not a valid Boolean value. Defaulting to false");
            }
            if (canUpdate)
            {
                Client.GuildAvailable += Client_GuildAvailable;
                autoUpdateThread = new Thread(AutoUpdateLoop);
                BeginAutoUpdateLoop();
                Console.WriteLine("INIT: Done, AutoUpdate started successfully!");
            }
        }

        private Queue<SocketGuild> awaitingUpdates = new Queue<SocketGuild>();
        private async Task Client_GuildAvailable(SocketGuild arg)
        {
            Console.WriteLine("INIT: Refreshing Guild..." );
            await PopulateChannels(arg);
            Client.ChannelCreated += Client_ChannelCreated;
            //await UpdateChannels(arg);
        }

        private async Task Client_ChannelCreated(SocketChannel arg)
        {
            await UpdateChannels((arg as SocketVoiceChannel).Guild);
        }

        public bool BeginAutoUpdateLoop()
        {
            if (!canUpdateAutomatically || autoUpdateThread.IsAlive)
                return false;
            autoUpdateThread.Start();
            return true;
        }

        private async void AutoUpdateLoop()
        {
            canUpdateAutomatically = true;
            int timeout = 10000;
            try
            {
                var frequency = Configuration.ConfigManager.GetValue(Constants.ServerQueryFrequency);
                timeout = int.Parse(frequency);
            }
            catch (Exception)
            {
                timeout = 10000;
                Console.WriteLine($"Incorrect config setting for AutoUpdateFrequency! Using {timeout}ms by default...");
            }
            Console.WriteLine("AutoUpdating every " + timeout + "ms");
            while (canUpdateAutomatically)
            {
                try
                {
                    foreach (var guild in Client.Guilds)                    
                        await UpdateChannels(guild); //update each guild once per timeout        
                    await UpdateUserStatus();
                    await UpdateMaintainance();
                }
                catch (Exception e)
                {
#if DEBUG
                    throw e;
#endif
                }
                Thread.Sleep(timeout);
            }
        }

        public void EndAutoUpdateLoop()
        {
            canUpdateAutomatically = false;
            autoUpdateThread.Abort();
            autoUpdateThread = new Thread(AutoUpdateLoop);
        }

        public async Task ClearChannels(SocketCategoryChannel channel)
        {
            foreach (var c in channel.Channels)
                if (c is SocketVoiceChannel)
                    await c.DeleteAsync();
        }

        public void SetNotifyChannel(SocketGuildChannel channel)
        {
            Constants.SetNotifyChannel(channel);
        }
        public void SetModRole(SocketRole Role)
        {
            Constants.SetModerationRole(Role);
        }
        public void ClearScheduled()
        {
            Configuration.ConfigManager.StoreSetting(new Configuration.KeyValuePair(Constants.ScheduledRestart, "NULL")); //clear maintainance
            maintainanceNotificationSent = false;
        }

        public SocketTextChannel GetNotifyChannel(SocketGuild guild)
        {
            var guildChan = Constants.GuildNotifyChannels.Keys.FirstOrDefault(x => x == guild.Id);
            if (guildChan == default)
                return null;
            return (SocketTextChannel)guild.GetChannel(Constants.GuildNotifyChannels[guildChan]);
        }
        public SocketRole GetModRole(SocketGuild guild)
        {
            var guildChan = Constants.GuildModRoles.Keys.FirstOrDefault(x => x == guild.Id);
            if (guildChan == default)
                return null;
            return guild.GetRole(Constants.GuildModRoles[guildChan]);
        }
        
        private async Task<SocketCategoryChannel> GetOrCreateStatusCategory(SocketGuild guild)
        {
            var header = Configuration.ConfigManager.GetValue(Constants.CategoryHeader);
            var category = guild.CategoryChannels.Where(x => x.Name == header).FirstOrDefault();
            if (category == null)
            {
                var id = (await guild.CreateCategoryChannelAsync(header)).Id;                
                category = guild.GetCategoryChannel(id);
            }
            if (category == null)
                return null; //for some reason it wasn't created?
            return category;
        }

        public async Task<bool> PopulateChannels(SocketGuild guild)
        {
            var channels = new ulong[2];
            var category = await GetOrCreateStatusCategory(guild);
            if (category == null)
                throw new Exception("Category was not able to be created!");
            await ClearChannels(category);
            var channel = await guild.CreateVoiceChannelAsync("🌄City Status: ???",
                (Discord.VoiceChannelProperties p) =>
                {
                    p.CategoryId = category.Id;
                    p.UserLimit = 0;
                });
            var permissions = new OverwritePermissions(connect: PermValue.Deny);
            await channel.AddPermissionOverwriteAsync(Client.CurrentUser, new OverwritePermissions(connect: PermValue.Allow));
            await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, permissions);
            channels[0] = channel.Id;
            channel = await guild.CreateVoiceChannelAsync($"🌐🏡: ??? 👥: ???",
                (Discord.VoiceChannelProperties p) =>
                {
                    p.CategoryId = category.Id;
                    p.UserLimit = 0;
                });
            await channel.AddPermissionOverwriteAsync(Client.CurrentUser, new OverwritePermissions(connect: PermValue.Allow));
            await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, permissions);     
            channels[1] = channel.Id;
            if (statusUpdateChannels.ContainsKey(guild.Id))
                statusUpdateChannels[guild.Id] = channels;
            else
                statusUpdateChannels.Add(guild.Id, channels);
            return true;
        }        

        public async Task<Embed> GetStatusEmbed(bool outage = false)
        {
            var embed = new EmbedBuilder()
                .WithColor(new Color(0x42f58a))
                .WithTimestamp(DateTimeOffset.Now)
                .WithFooter(footer =>
                {
                    footer
                        .WithText("Online")
                        .WithIconUrl("https://cdn.discordapp.com/icons/244548040721956865/42225191175358a8b730469ec91391b7.png");
                })
                .WithAuthor(author =>
                {
                    author
                        .WithName("FreeSO Server Status")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            embed.WithImageUrl(ConfigManager.GetValue(Constants.StatusUpdateImg));
            var status = await FreeSOApi.GetFreeSOStatus();
            switch (status.IsOnline)
            {
                case true:
                    embed.Title = "FreeSO is Online";
                    if (!outage)
                        embed.Description = "**Players Online:** " + status.PlayersOnline + "\n" +
                            "**Lots Open:** " + status.LotsOnline;
                    else
                        embed.Description = "FreeSO is back online! If you're still having issues logging in/playing, please be patient. Have fun!";
                    embed.WithFooter(footer =>
                        {
                            footer
                                .WithText("Online")
                                .WithIconUrl("https://cdn.discordapp.com/icons/244548040721956865/42225191175358a8b730469ec91391b7.png");
                        });
                    break;
                case false:
                    if (status.IsMaintainanceActive)
                    {
                        embed.WithColor(Color.DarkBlue);
                        embed.Title = "Maintainance Active";
                        embed.Description = "FreeSO is offline for maintainance. You will not be able to login until service is restored. " +
                            "Keep an eye out here for updates!";
                    }
                    else
                    {
                        embed.WithColor(Color.DarkRed);
                        embed.Title = "FreeSO is Offline";
                        embed.Description = "You may experience trouble logging in. Service should be restored shortly!";
                    }
                    embed.WithFooter(footer =>
                        {
                            footer
                                .WithText("Offline")
                                .WithIconUrl("https://cdn.discordapp.com/icons/244548040721956865/42225191175358a8b730469ec91391b7.png");
                        });
                    break;
            }
            return embed.Build();
        }

        public async Task<Embed> GetMaintainanceSoonEmbed(int minUntilRestart)
        {
            var embed = new EmbedBuilder()
               .WithColor(Color.DarkGrey)
               .WithFooter(footer =>
               {
                   footer
                       .WithText("Maintainance")
                       .WithIconUrl("https://cdn.discordapp.com/icons/244548040721956865/42225191175358a8b730469ec91391b7.png");
               })
               .WithAuthor(author =>
               {
                   author
                       .WithName("Server Maintainance Incoming")
                       .WithUrl("https://freeso.org")
                       .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
               })
               .WithDescription($"FreeSO will be undergoing scheduled maintainance in {minUntilRestart} " +
               $"minutes. When it begins, you will not be able to login or continue playing. " +
               $"Don't worry, it should not last too long!");
            return embed.Build();
        }

        public async Task UpdateMaintainance()
        {
            var status = await FreeSOApi.GetFreeSOStatus();
            bool showAlert = (lastServerStatus == ServerStatus.ONLINE && !status.IsOnline) || 
                             (lastServerStatus == ServerStatus.OFFLINE && status.IsOnline);
            if (showAlert)
            {
                if (SilentMode)
                    return;
                if (!await FreeSOApi.CheckClientInternetStatus())
                    return;
                if (status.IsMaintainanceActive && !status.IsOnline)
                {
                    try
                    {
                        var scheduled = DateTime.Parse(Configuration.ConfigManager.GetValue(Constants.ScheduledRestart));
                        var announced = bool.Parse(Configuration.ConfigManager.GetValue(Constants.MaintainanceAnnounced));
                        if (!announced)
                        {
                            ConfigManager.StoreSetting(new Configuration.KeyValuePair(Constants.MaintainanceAnnounced, "true"));
                            try
                            {
                                foreach (var tuple in Constants.GuildNotifyChannels)
                                {
                                    var guildId = tuple.Key;
                                    var guild = Client.GetGuild(guildId);
                                    var channel = GetNotifyChannel(guild);
                                    if (channel == null)
                                        continue;
                                    await channel.SendMessageAsync(null, false, await GetStatusEmbed());
                                }
                                ClearScheduled();
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("FATAL: Cannot find notifications channel!");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("FATAL: One or more config settings is not of type Bool(ScheduleAnnounced), Int(Remind), or DateTime(ScheduledRestart). Cannot announce outages!");
                    }
                }
                else
                {
                    try
                    {
                        foreach (var tuple in Constants.GuildNotifyChannels)
                        {
                            var guildId = tuple.Key;
                            var guild = Client.GetGuild(guildId);
                            var channel = GetNotifyChannel(guild);
                            if (channel == null)
                                continue;
                            await channel.SendMessageAsync(null, false, await GetStatusEmbed(true));
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("FATAL: Cannot find notifications channel!");
                    }
                }
            }
            else
            {
                try
                {
                    var scheduled = DateTime.Parse(Configuration.ConfigManager.GetValue(Constants.ScheduledRestart));
                    var remind = int.Parse(Configuration.ConfigManager.GetValue(Constants.RemindBefore));
                    if (DateTime.UtcNow.AddMinutes(remind) > scheduled && !maintainanceNotificationSent)
                    {
                        foreach (var tuple in Constants.GuildNotifyChannels)
                        {
                            var guildId = tuple.Key;
                            var guild = Client.GetGuild(guildId);
                            var channel = GetNotifyChannel(guild);
                            if (channel == null)
                                continue;
                            await channel.SendMessageAsync(null, false, await GetMaintainanceSoonEmbed(remind));
                        }
                        maintainanceNotificationSent = true;
                    }
                }
                catch (Exception)
                {
                    //Console.WriteLine("FATAL: Cannot find notifications channel!");
                }
            }
            lastServerStatus = status.IsOnline ? ServerStatus.ONLINE : ServerStatus.OFFLINE;
        }

        public async Task UpdateUserStatus()
        {
            var status = await FreeSOApi.GetFreeSOStatus();
            await Client.SetStatusAsync(status.IsOnline ? UserStatus.Online : (status.IsMaintainanceActive) ? UserStatus.AFK : UserStatus.DoNotDisturb);
            await Client.SetGameAsync(CityStatusMessage + " // " + PlayerStatusMessage.Remove(0,2));
        }

        public async Task UpdateChannels(SocketGuild guild)
        {
            var category = await GetOrCreateStatusCategory(guild);
            if (category == null)
                return;
            var freeSOStatus = await FreeSOApi.GetFreeSOStatus();
            int i = 1;            
            foreach(var channel in category.Channels.Where(x => statusUpdateChannels[guild.Id].Contains(x.Id)).OrderBy(x => x.Position))
            {
                switch (i)
                {
                    case 1:
                        await channel.ModifyAsync((GuildChannelProperties p) =>
                        {
                            p.Name = CityStatusMessage;
                        });
                        break;
                    case 2:
                        await channel.ModifyAsync((GuildChannelProperties p) =>
                        {
                            p.Name = PlayerStatusMessage;
                        });                        
                        break;
                    default: return;
                }
                i++;
            }
        }
    }
}
