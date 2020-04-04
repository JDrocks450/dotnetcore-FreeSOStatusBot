using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    /// <summary>
    /// Creates, Updates, and Manages the audio channel statistics for this bot
    /// </summary>
    public class FreeSOChannelModule : ModuleBase<SocketCommandContext>
    {     
        [Command("force-stopstatusupdate")]
        [Summary("Warning! Affects all discords this bot is in. Immediately stops autoupdating server status.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task StopAutoUpdate()
        {
            Console.WriteLine("AutoUpdate Force Stopped...");
            if (!FreeSOStatusProvider.Get().IsAutoUpdating)
            {
                await ReplyAsync("The Auto-Update service not running");
                return;
            }
            FreeSOStatusProvider.Get().EndAutoUpdateLoop();          
        }
        [Command("force-startstatusupdate")]
        [Summary("Immediately starts autoupdating server status.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task StartAutoUpdate()
        {
            Console.WriteLine("AutoUpdate Force Started...");
            if (FreeSOStatusProvider.Get().IsAutoUpdating)
            {
                await ReplyAsync("The Auto-Update service is already running");
                return;
            }
            FreeSOStatusProvider.Get().BeginAutoUpdateLoop();
        }

        [Command("setup", RunMode=RunMode.Async)]
        [Summary("Forces the bot to add all channels that it needs to this discord. *Deprecated*")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task RunSetup()
        {
            var result = await FreeSOStatusProvider.Get().PopulateChannels(Context.Guild);
            await ReplyAsync("Populated! (Remember: You can change the category name in config) :white_check_mark:");
        }

        [Command("purge", RunMode=RunMode.Async)]
        [Summary("Forces the bot to delete all channels that it created in this discord. *Deprecated*")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task Clean()
        {
            var header = Configuration.ConfigManager.GetValue(Constants.CategoryHeader);
            var category = Context.Guild.CategoryChannels.FirstOrDefault(x => x.Name.Equals(header,StringComparison.OrdinalIgnoreCase));
            int i = 0;
            if (category != null)
            {
                await FreeSOStatusProvider.Get().ClearChannels(category);
                await category.DeleteAsync();
                i++;
            }
            await ReplyAsync($"Deleted {i} categories! :white_check_mark:");
        }

        [Command("silent")]
        [Summary("The bot will no longer post any server status updates until silent mode is turned off.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SilentModeToggle(string mode)
        {
            mode = mode.ToLower();
            if (mode == "on" || mode == "true")
                FreeSOStatusProvider.Get().SilentMode = true;
            else if(mode == "off" || mode == "false")
                FreeSOStatusProvider.Get().SilentMode = false;
            await SilentModeToggle();
        }
        [Command("silent")]
        [Summary("Checks the current SilentMode status.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SilentModeToggle()
        {
            var val = FreeSOStatusProvider.Get().SilentMode;
            var str = "off";
            if (val)
                str = "on";
            await ReplyAsync("**Silent Mode** is currently turned: " + str);
        }

        [Command("force-update")]
        [Summary("Forces the bot to immediately check the server status of FreeSO.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task ForceUpdate()
        {
            await FreeSOStatusProvider.Get().UpdateChannels(Context.Guild);
            await ReplyAsync("Updated! :white_check_mark:");
        }
    }
}
