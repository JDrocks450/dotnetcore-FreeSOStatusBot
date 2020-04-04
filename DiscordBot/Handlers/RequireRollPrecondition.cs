using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public enum UserStanding
    {
        //EVERYONE,
        MODERATOR,
        SUPERADMIN
    }
    public class RequireRoleAttribute : PreconditionAttribute
    {
        private readonly UserStanding _standing;

        public RequireRoleAttribute(UserStanding standing)
        {
            _standing = standing;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            CommandInfo command, IServiceProvider services)
        {
            var result = CheckModClearance(context.User, _standing);
            if (result.Item1 == true)
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError(result.Item2);
        }

        public static (bool, string) CheckModClearance(IUser User, UserStanding Level)
        {
            var guildUser = User as IGuildUser;
            if (guildUser == null)
                return (false, "This command cannot be executed outside of a guild.");
            if (guildUser.GuildPermissions.Administrator)
                return (true,"");
            var guild = guildUser.Guild;
            var _roleId = TranslateToRoleID(Level, guildUser.Guild);
            if (_roleId == null)
                return (false, "That guild is not stored in the database. I have no idea what the moderation role is!");
            var baseline = guild.Roles.FirstOrDefault(r => r.Id == _roleId);
            if (baseline == null)
                return (false, $"The guild does not have the role ({_roleId}) required to access this command.");                    
            var heirarchy = guild.Roles.Where(x => x.Position >= baseline.Position).Select(y => y.Id);
            foreach (var tier in heirarchy)
                if (guildUser.RoleIds.Any(rId => rId == tier))
                    return (true,"");
            return (false,"You do not have the sufficient role required to access this command.");
        }

        private static ulong? TranslateToRoleID(UserStanding standing, IGuild guild)
        {
            switch (standing)
            {
                case UserStanding.MODERATOR:
                    if (Constants.GuildModRoles.Keys.Contains(guild.Id))
                    {
                        return Constants.GuildModRoles[guild.Id];
                    }
                    else
                        return null;
                case UserStanding.SUPERADMIN:
                    return null;
            }
            return null;
        }
    }
}
