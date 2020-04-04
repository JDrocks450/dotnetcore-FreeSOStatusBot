using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Configuration;
using DiscordBot.Handlers;
using Newtonsoft.Json;

namespace DiscordBot.Modules
{
    public class CommandModule : ModuleBase<SocketCommandContext>
    {
        /*
        [Command("hello", RunMode = RunMode.Async)]
        [Summary("Says hi! The baseline test to see if the bot is resposive.")]
        public async Task Hello()
        {
            await ReplyAsync("Hi!");
        }

        [Command("say", RunMode = RunMode.Async)]
        [Summary("Says whatever you say. Tests if the bot is working.")]
        public async Task Say()
        {
            await ReplyAsync("Say what? :)");
        }
        [Command("say", RunMode = RunMode.Async)]
        [Summary("Says whatever you say. Tests if the bot is working.")]
        public async Task Say([Remainder]string message)
        {
            await ReplyAsync("You really think I'm going to say... \n" +
                "*" + message + "*\n" +
                "Oh no!");
        }
        [Command("question", RunMode = RunMode.Async)]
        [Summary("Quirky doe.")]
        public async Task Question([Remainder]string message)
        {
            if (Context.User.Id == 84355933311864832)
                await ReplyAsync("Please keep talking because I cannot understand speech but I will gladly listen.");
            else
                await ReplyAsync("Shut up I'm so sick of your attitude.");
        }
        [Command("dateme", RunMode = RunMode.Async)]
        [Summary("lmao")]
        public async Task DateMe()
        {
            if (Context.User.Id == 140654419191529472)
                await ReplyAsync("Sorry we're only friends.");
            else
                await ReplyAsync("I'm only 3 days old... and I'm taken anyway.");
        }
        */

        [Command("help", RunMode = RunMode.Async)]
        [Summary("Lists all commands available to users")]
        public async Task HelpMeICantSwim([Remainder]string[] args = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Magenta)
                .WithAuthor(author =>
                {
                    author
                        .WithName("Command Help")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            bool isMod = RequireRoleAttribute.CheckModClearance(Context.User, UserStanding.MODERATOR).Item1;
            if (args == null)
            {
                if (isMod)
                {
                    var modRole = FreeSOStatusProvider.Get().GetModRole(Context.Guild);
                    embed.Description += $"**These actions require minimum role: {modRole?.Name ?? "[Not set yet]"} (use: ``mod`` command)**\n \n";
                    foreach (var pair in Constants.ModCommandSummaries)
                    {
                        embed.Description += $"     ``{pair.Key.TrimStart('*')}:`` {pair.Value}\n\n";
                    }
                }
                embed.Description += $"**These actions are available to everyone:**\n \n";
                foreach(var pair in Constants.GeneralCommandSummaries)
                {
                    embed.Description += $"     ``{pair.Key.TrimStart('*')}:`` {pair.Value}\n\n";
                }
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            if (isMod && Constants.ModCommandSummaries.TryGetValue(args[0], out var value)) {
                embed.Description += $"**Mod Only**: ``{args[0]}:`` {value}\n";
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            else if (Constants.GeneralCommandSummaries.TryGetValue(args[0], out value)) {
                 embed.Description += $"``{args[0]}:`` {value}\n";
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            if (isMod)
                await ReplyAsync("**Error:** Couldn't find that command");
        }
        private async Task ListConfigStrings([Remainder]string[] args = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.DarkBlue)
                .WithAuthor(author =>
                {
                    author
                        .WithName("Configuration Help")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            if (args == null)
            {
                embed.Description += "*These are the names of every editable setting, with its usage description.*\n \n";
                foreach(var pair in Constants.ConfigStrings)
                {
                    embed.Description += $"     **{pair.Key}:** {pair.Value} *Currently: {Constants.ConfigValues[pair.Key]}*\n\n";
                }
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            if (Constants.ModCommandSummaries.TryGetValue(args[0], out var value)) {
                embed.Description += $"**{args[0]}:** {value}\n";
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            await ReplyAsync("*Error:* Couldn't find that command!");
        }

        [Command("set", RunMode =RunMode.Async)]
        [Summary("Views all editable settings")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetList()
        {
            await ListConfigStrings();
        }

        [Command("set", RunMode =RunMode.Async)]
        [Summary("*[settingName] [newValue]* Sets the specified setting to the specified value. *Check the expected type before setting the new value!*")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetSetting(string settingName, [Remainder]string value)
        {
            var embed = new EmbedBuilder();
            embed.WithAuthor(author =>
                {
                    author
                        .WithName("Configuration Editor")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            async Task replyError(string reason)
            {
                embed.WithColor(Color.Red).WithTitle("Formatting Error - " + reason).
                                    WithDescription("**Reminder:** Passing *list* when calling the *set* command lists all settings you can configure.");
                await ReplyAsync(null, false, embed.Build());
            }            
            if (string.IsNullOrWhiteSpace(value))
            {
                await replyError("Two Parameters Required");
                return;
            }
            string arg1 = settingName, arg2 = value;
            if (string.IsNullOrWhiteSpace(arg1) || string.IsNullOrWhiteSpace(arg2))
            {
                await replyError("Parameter(s) Blank");
                return;
            }
            if (!Constants.ConfigStrings.TryGetValue(arg1, out _))
            {                
                await replyError("Unrecognized Setting Name");
                return;            
            }
            ConfigManager.StoreSetting(new KeyValuePair(arg1, arg2));
            embed.WithColor(Color.Blue).WithTitle("Setting Changed").
                    WithDescription($"Setting: **{arg1}** was changed to: **{arg2}**. \n*Changes are automatically saved.*").WithCurrentTimestamp();
            await ReplyAsync(null, false, embed.Build());
        }
        [Command("mod", RunMode = RunMode.Async)]
        [Summary("Views the minimum role required to use mod commands.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetModRole()
        {
            var channel_ = FreeSOStatusProvider.Get().GetModRole(Context.Guild);
            await ReplyAsync("The current **Moderation Role** is: " + (channel_?.Name ?? "None"));
            return;
        }

        [Command("mod", RunMode = RunMode.Async)]
        [Summary("*[roleName(optional)]* Sets the minimum role that can use mod commands.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetModRole(string name)
        {
            async void replyError()
            {
                await ReplyAsync("**Error:** Pass a valid channel name that server notifications should be sent.");
            }      
            var role = Context.Guild.Roles.Where(x => x.Name == name).FirstOrDefault();
            if (role == null)
            {
                replyError();
                return;
            }
            FreeSOStatusProvider.Get().SetModRole(role);
            await ReplyAsync("**Success:** The Role: " + role.Name + " and above can now use mod commands.");            
        }        
        [Command("status-channel", RunMode = RunMode.Async)]
        [Summary("Views the channel to send server status updates to.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetStatusUpdatesChannel()
        {
            var channel_ = FreeSOStatusProvider.Get().GetNotifyChannel(Context.Guild);
            await ReplyAsync("The current **Notifications Channel** is: " + (channel_?.Name ?? "None"));
            return;
        }

        [Command("status-channel", RunMode = RunMode.Async)]
        [Summary("*[channelName(optional)]* Sets the channel to send server status updates to.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task SetStatusUpdatesChannel(string name)
        {
            async void replyError()
            {
                await ReplyAsync("**Error:** Pass a valid channel name that server notifications should be sent.");
            }      
            var channel = Context.Guild.Channels.Where(x => x.Name == name).FirstOrDefault();
            if (channel == null)
            {
                replyError();
                return;
            }
            FreeSOStatusProvider.Get().SetNotifyChannel(channel);
            await ReplyAsync("**Success:** Server notifications will be posted in: #" + channel.Name);            
        }        
        [Command("status", RunMode = RunMode.Async)]
        [Summary("Views the current status of FreeSO")]
        public async Task GetStatus()
        {            
            await ReplyAsync(null, false, await FreeSOStatusProvider.Get().GetStatusEmbed());
        }
        [Command("online")]
        [Summary("Lists all online lots in order from most to least people.")]
        public async Task GetAllOnline()
        {
            var list = await FreeSOApi.GetAllOnline();          
            var embed = new EmbedBuilder();
            if (list == null)
                return;
            var success = list[0].success;
            embed.WithAuthor(author =>
                {
                    author
                        .WithName((list?.Length ?? 0) + " Lots Online!")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            //embed.WithThumbnailUrl(list[].lot_thumbnailurl);
            //var ownerInfo = await FreeSOApi.GetAvatarByID(status.owner_id);
            StringBuilder b = new StringBuilder();
            foreach(var house in list.OrderByDescending(x => x.avatars_in_lot).Take(20))
            {
                b.AppendLine("🏡 " + house.name + ": 👥: " + house.avatars_in_lot);
            }
            if (list.Length > 20)
                b.AppendLine($"*and {list.Length - 20} more...*");
            embed.WithDescription(b.ToString());
            embed.WithFooter("🏡:" + list.Length);
            embed.WithColor(Color.Green);
            await ReplyAsync(null, false, embed.Build());
        }

        [Command("lot")]
        [Summary("*[lotName]* Views the lot with the specified name.")]        
        public async Task GetLotByName([Remainder]string name)
        {
            var status = await FreeSOApi.GetLotByName(name);            
            var embed = new EmbedBuilder();
            var success = status.success;
            embed.WithAuthor(author =>
                {
                    author
                        .WithName(success ? "Viewing " + status.name : $"Lot Not Found - \"{name}\"")
                        .WithUrl("https://freeso.org")
                        .WithIconUrl("https://cdn.discordapp.com/icons/139137125001068546/a_4e3d73893ca705df61c82571291e8e15.png");
                });
            embed.WithThumbnailUrl(status.lot_thumbnailurl);
            FreeSOAvatarInfo ownerInfo = default;
            ownerInfo.name = "Nobody";
            if (status.owner_id != 0)
                ownerInfo = await FreeSOApi.GetAvatarByID(status.owner_id);
            embed.WithDescription(success ? 
                $"**Owner:** {ownerInfo.name}\n" +
                $"**Created:** {status.Created.ToShortDateString()}" + ((status.description != "") ?
                $"\n\n*{status.description}*" : "")
                : "That lot could not be found. Make sure you spelled the name exactly as it appears in-game.");
            embed.WithFooter("👥: " + status.avatars_in_lot);
            if (success)
                embed.WithColor(status.isOnline ? Color.Green : Color.DarkGrey);
            else
                embed.WithColor(Color.Red);
            await ReplyAsync(null, false, embed.Build());
        }

        [Command("schedule", RunMode = RunMode.Async)]
        [Summary("*[utcTime] [remindBefore(optional)]* Pass a date/time (or say \"now\") and optionally an amount of time to notify before the time given.")]
        [RequireRole(UserStanding.MODERATOR)]
        public async Task ScheduleRestart([Remainder]string[] args = null)
        {
            var embed = new EmbedBuilder();
            var scheduled = ConfigManager.GetValue(Constants.ScheduledRestart);
            if (scheduled == "NULL")
                scheduled = null;
            if (args == null)
            {                                                                
                embed.WithColor(Color.Orange);
                embed.Title = $"**There is {(scheduled == null ? "no" : "one")} scheduled server restart.**";
                if (scheduled != null)
                {
                    var remind = int.Parse(ConfigManager.GetValue(Constants.RemindBefore));
                    var str = scheduled;
                    embed.Description = scheduled + ((remind > -1) ? (", Reminding " + remind + " minutes before") : "");
                }
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            if (args[0].ToLower() == "clear")
            {
                FreeSOStatusProvider.Get().ClearScheduled();
                embed.WithColor(Color.Blue);
                embed.Title = "Restart Cleared";
                embed.Description = "The scheduled restart has been cleared.";
                await ReplyAsync(null, false, embed.Build());
                return;
            }
            try
            {
                var date = DateTime.UtcNow;
                if (args[0].ToLower() != "now")
                    date = DateTime.Parse(args[0]);
                var modifier = "";
                if (date < DateTime.UtcNow && args[0].ToLower() != "now")
                {
                    date = date.AddDays(1);
                    modifier = " - Tomorrow";
                }                
                if (scheduled != null)
                {
                    embed.WithColor(Color.Red);
                    embed.Title = "Restart Already Scheduled";
                    embed.Description = "Use the *clear* command to remove the scheduled restart first.";
                    await ReplyAsync(null, false, embed.Build());
                    return;
                }
                embed.WithColor(Color.Green);
                int remind = -1;
                if (args.Length > 1)
                {
                    if (!int.TryParse(args[1], out remind))
                    {
                        remind = -1;
                    }
                    else modifier += ", Reminding " + remind + " Minutes Before";
                }
                embed.Title = "Restart Scheduled" + modifier;
                embed.Description = "**Success:** Server Restarting on: " + date + "(UTC)";
                await ReplyAsync(null, false, embed.Build());
                ConfigManager.StoreSetting(new KeyValuePair(Constants.ScheduledRestart, date.ToString()));
                ConfigManager.StoreSetting(new KeyValuePair(Constants.RemindBefore, remind.ToString()));
                ConfigManager.StoreSetting(new KeyValuePair(Constants.MaintainanceAnnounced, "false"));
                return;
            }
            catch (Exception)
            {
                embed.WithColor(Color.Red);
                embed.WithTitle("Error in Formatting");
                embed.Description = "Format: *[date/time] [notify = 15]* Enter a date/time " +
                    "(according to .NET DateTime), and a time (in minutes) to alert users before restarting.";
                await ReplyAsync(null, false, embed.Build());
                return;
            }    
        }
    }
}
