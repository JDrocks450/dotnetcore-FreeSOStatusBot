using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Readers
{
    public class StringArrayReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            string[] result;
            var message = input;
            result = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (result.Length > 0)
            {
                List<string> newArr = new List<string>();
                bool lookingForStart = true;
                string start = "";
                foreach(var str in result)
                {
                    if (str.StartsWith('"') && lookingForStart)
                    {
                        lookingForStart = false;
                        start = str;
                        if (str.EndsWith('"') && !lookingForStart)
                        {
                            newArr.Add(str.Replace('"', ' '));
                            lookingForStart = true;
                        }
                    }
                    else if (str.EndsWith('"') && !lookingForStart)
                    {
                        newArr.Add((start + " " + str).Replace('"', ' ').Trim(' '));
                        lookingForStart = true;
                    }
                    else if (lookingForStart) newArr.Add(str);
                    else start += str;
                }
                result = newArr.ToArray();
                return Task.FromResult(TypeReaderResult.FromSuccess(result));
            }
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a String[]."));
        }
    }
}
