using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace DiscordBot
{    
    public static class Constants
    {
        public static Dictionary<ulong, ulong> GuildNotifyChannels = new Dictionary<ulong, ulong>();
        public static Dictionary<ulong, ulong> GuildModRoles = new Dictionary<ulong, ulong>();

        public readonly static Dictionary<string, string> ModCommandSummaries = new Dictionary<string, string>();
        public readonly static Dictionary<string, string> GeneralCommandSummaries = new Dictionary<string, string>();
        /*
        {
            { "schedule", "*[date/time] [remindOffset]* -- Enter a date/time " +
                    "(according to .NET DateTime), and a time (in minutes) to alert users before restarting. *Clearable*" },
            { "reason","*[reason]* -- Sets a reason for future server outages. *Clearable*" },
            { "eta","*[eta]* -- Sets an estimated time until the server is restored. *Clearable*"},
            { "set-img", "*[online/offline][url]* -- The image url to use for server online/offline status updates. *Clearable*"},
            { "status-channel","*[channel]* The name of the channel to post server status updates into. *Clearable*"},
            { "status", "Gets the current server status." },
            { "*command* [clear]", "Passing *clear* into any command that is marked *Clearable* will clear its value."}
        };
        Keeping this here in case any of you snoopers like to see how my workflow works because this implementation here is terrible don't do it :)
        */ 
        public readonly static Dictionary<string, string> ConfigStrings = new Dictionary<string, string>();
        public readonly static Dictionary<string, string> ConfigValues = new Dictionary<string, string>();  
        
        [ImmutableObject(true)] //check for this in future
        public const string ConfigKeyToken = "DiscordToken";
        [Description("The prefix this bot responds to.")]
        public const string Prefix = "Prefix";
        //[Description("The roles that are allowed to make changes to this bot.")]
        public const string AdminRole = "Role";
        //[Description("The channel to send announcements to.")]
        public const string NotifyChannel = "NotifyChannel";
        [Description("The scheduled restart. *Please use schedule command!*")]
        public const string ScheduledRestart = "ScheduledRestart";
        public const string MaintainanceAnnounced = "ScheduleReported";
        [Description("The time before announcing the restart. *(in minutes!)*")]
        public const string RemindBefore = "Remind";
        [Description("The header for the FreeSO Status category.")]
        public const string CategoryHeader = "StatusCategoryHeader";
        [Description("The amount of time in between querying the server's status from the userapi *(in milliseconds!)*.")]
        public const string ServerQueryFrequency = "ServerQueryFrequency";
        [Description("Determines whether the server will automatically populate channels for showing server status when restarted.")]
        public const string AutoSetupAllowed = "AutoSetup";
        [Description("The URL to use when interacting with the FreeSO Api")]
        public const string FSOJsonURL = "FreeSOApiUrl";
        [Description("The URL to use when showing an image in server status updates. *An invalid address will show no image.*")]
        public const string StatusUpdateImg = "StatusUpdateImgUrl";

        public static void SetNotifyChannel(SocketGuildChannel channel)
        {
            var settingStr = "";
            if (GuildNotifyChannels.Keys.Contains(channel.Guild.Id))
                GuildNotifyChannels[channel.Guild.Id] = channel.Id;
            else
                GuildNotifyChannels.Add(channel.Guild.Id, channel.Id);
            foreach(var tuple in GuildNotifyChannels)
            {
                settingStr += tuple.Key + ":" + tuple.Value;
            }
            ConfigManager.StoreSetting(new Configuration.KeyValuePair(Constants.NotifyChannel, settingStr));
            Console.WriteLine("A Notify Channel for:" + channel.Guild + " has been added: " + channel.Name + ". Settings saved.");
        }
        public static void SetModerationRole(SocketRole role)
        {
            var settingStr = "";
            if (GuildModRoles.Keys.Contains(role.Guild.Id))
                GuildModRoles[role.Guild.Id] = role.Id;
            else
                GuildModRoles.Add(role.Guild.Id, role.Id);
            foreach(var tuple in GuildModRoles)
            {
                settingStr += tuple.Key + ":" + tuple.Value;
            }
            ConfigManager.StoreSetting(new Configuration.KeyValuePair(Constants.AdminRole, settingStr));
            Console.WriteLine("A Mod Role for:" + role.Guild + " has been added: " + role.Name + ". Settings saved.");
        }

        static Constants()
        {
            Console.WriteLine("INIT: Caching Constants for Configuration Editior");
            foreach(var field in typeof(Constants).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                try
                {
                    var attribute = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attribute != null) {
                        var name = (string)field.GetValue(null);
                        ConfigStrings.Add(name, attribute.Description);
                        ConfigValueUpdated(name, Configuration.ConfigManager.GetValue(name));
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }     
            Console.WriteLine("INIT: Interpreting Notification Channels");
            var notifyChannels = ConfigManager.GetValue(NotifyChannel).Split(",", StringSplitOptions.RemoveEmptyEntries);
            foreach (var str in notifyChannels)
            {
                var guildId = ulong.Parse(str.Substring(0, str.IndexOf(':')));
                var channelId = ulong.Parse(str.Substring(str.IndexOf(':') + 1));
                GuildNotifyChannels.Add(guildId, channelId);
            }
            Console.WriteLine("INIT: Restoring Moderation Roles");
            var modRoles = ConfigManager.GetValue(AdminRole).Split(",", StringSplitOptions.RemoveEmptyEntries);
            foreach (var str in modRoles)
            {
                var guildId = ulong.Parse(str.Substring(0, str.IndexOf(':')));
                var roleId = ulong.Parse(str.Substring(str.IndexOf(':') + 1));
                GuildModRoles.Add(guildId, roleId);
            }
            Console.WriteLine("INIT: done.");
        }

        public static void SubmitCommandInfo(IEnumerable<CommandInfo> CommandInfoSource)
        {
            int unspecified = 0;
            foreach(var command in CommandInfoSource)
            {
                int loops = 0;
                string modifier = "";
                while (true)
                {
                    try
                    {
                        if (loops > 5)
                            throw new Exception("Can't add to help list.. too many overloads! Command: " + command.Name);
                        (command.Preconditions.Any() ? ModCommandSummaries : GeneralCommandSummaries).Add
                            (command.Name + modifier, string.IsNullOrWhiteSpace(command.Summary) ? "No summary given. :(" : command.Summary);
                        break;
                    }
                    catch (ArgumentException)
                    {
                        loops++;
                        modifier = $"(overload: {loops})";
                    }
                }
                if (string.IsNullOrWhiteSpace(command.Summary))
                    unspecified++;
            }
            Console.WriteLine($"Cached {CommandInfoSource.Count()} command summaries, {unspecified} of which have no summary for some reason.");
        }

        public static void ConfigValueUpdated(string key, string value)
        {
            ConfigValues[key] = value;
        }
    }
}
