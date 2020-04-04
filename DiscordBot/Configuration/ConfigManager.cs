using System;
using System.Configuration;

namespace DiscordBot.Configuration
{
    public class ConfigManager : IConfiguration
    {
        public string GetValueFor(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public void SetValueFor(string key, string value)
        {
            StoreSetting(new KeyValuePair(key, value));
        }

        public static string GetValue(string key)
        {
            try
            {
                return ConfigurationManager.AppSettings[key];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void StoreSetting(KeyValuePair setting)
        {
            var editor = new ConfigurationFileEditor();
            editor.WriteSetting(setting);
            editor.Save();
            Constants.ConfigValueUpdated(setting.Key, setting.Value);
        }
    }
}
