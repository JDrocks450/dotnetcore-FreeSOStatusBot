using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class FreeSOServerStatus
    {
        public bool IsOnline, IsMaintainanceActive;
        public int PlayersOnline, LotsOnline;
        public DateTime ApiTimeStamp, PingTimeStamp;
    }
    public struct FreeSOLotInfo
    {
        [JsonIgnore]
        public string lot_thumbnailurl;
        [JsonIgnore]
        public bool isOnline;
        [JsonIgnore]
        public bool success;
        public uint lot_id;
        public uint shard_id;
        public uint owner_id;
        public uint[] roommates;
        public uint avatars_in_lot;
        public string name;
        public string description;
        public ulong location;
        public uint neighborhood_id;
        public long created_date;
        public DateTime Created => FreeSOApi.UnixTimeStampToDateTime(created_date);

        public async Task<FreeSOAvatarInfo> GetOwnerInfo()
        {
            return await FreeSOApi.GetAvatarByID(owner_id);
        }        
    }
    public struct FreeSOAvatarInfo
    {
        public uint avatar_id;
        public byte gender;
        public ulong date;
        public uint current_job;
        public string lot_thumbnailurl;
        public uint lot_id;
        public uint shard_id;
        public string name;
        public string description;
        public DateTime Created => FreeSOApi.UnixTimeStampToDateTime(date);
    }
    /// <summary>
    /// A class for interacting with the FreeSO API
    /// </summary>
    public class FreeSOApi
    {
        private static FreeSOServerStatus CachedStatus;
        public static Uri UserApiAddress {
            get
            {
                try
                {
                    return new Uri("https://api.freeso.org");//return new Uri(Configuration.ConfigManager.GetValue(Constants.FSOJsonURL));
                }
                catch (UriFormatException)
                {
                    return new Uri("https://api.freeso.org");
                }
            }
        }
        private static System.Net.Http.HttpClient freeSOStatusClient = new System.Net.Http.HttpClient() {
            BaseAddress = UserApiAddress
        }; // reuse these even though they're disposable!        

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static async Task<bool> CheckClientInternetStatus(string pingAddr = "http://www.google.com")
        {
            try {
                using (var packet = await freeSOStatusClient.GetAsync(new Uri(pingAddr)))
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<FreeSOServerStatus> GetFreeSOStatus()
        {
            if (!int.TryParse(Configuration.ConfigManager.GetValue(Constants.ServerQueryFrequency), out int freq)) {
                freq = 10000;
                Console.WriteLine($"Config Error! ServerQueryFrequency not a valid integer. Defaulting to {freq} seconds");
            }            
            if (CachedStatus != null && CachedStatus.ApiTimeStamp.AddMilliseconds(freq) > DateTime.UtcNow)
            {
                return CachedStatus;
            }
            int onlineLots = 0, players = 0;
            bool success = true;
            try
            {
                players = await GetOnlinePlayers();
                onlineLots = await GetOnlineLots();
            }
            catch (Exception)
            {
                success = false;
            }
            success = true;
            var maintainance = false;
            if (!success)
            {
                var scheduled = Configuration.ConfigManager.GetValue(Constants.ScheduledRestart);
                if (scheduled != null && scheduled != "NULL")
                {
                    var time = DateTime.Parse(scheduled);
                    maintainance = time <= DateTime.UtcNow;
                }
            }
            CachedStatus = new FreeSOServerStatus()
            {
                IsOnline = success,
                IsMaintainanceActive = maintainance,
                PlayersOnline = players,
                LotsOnline = onlineLots,
                ApiTimeStamp = DateTime.UtcNow,
                PingTimeStamp = DateTime.UtcNow
            };
            return CachedStatus;
        }

        public static async Task<int> GetOnlinePlayers()
        {
            using (var data = await freeSOStatusClient.GetAsync("userapi/avatars/online?compact=true"))
            {
                dynamic d = JsonConvert.DeserializeObject(await data.Content.ReadAsStringAsync());
                return d.avatars_online_count;
            }
        }

        public static async Task<int> GetOnlineLots()
        {
            using (var data = await freeSOStatusClient.GetAsync("userapi/city/001/lots/online?compact=true"))
            {
                dynamic d = JsonConvert.DeserializeObject(await data.Content.ReadAsStringAsync());
                return d.total_lots_online;
            }
        }        

        public static async Task<FreeSOAvatarInfo> GetAvatarByID(uint id)
        {
            using (var data = await freeSOStatusClient.GetAsync("/userapi/avatars/" + id))
            {
                return JsonConvert.DeserializeObject<FreeSOAvatarInfo>(await data.Content.ReadAsStringAsync());
            }
        }
        public static string GetLotThumbnail(ulong lotLocation)
        {
            return freeSOStatusClient.BaseAddress + $"userapi/city/1/{lotLocation}.png";
        }

        public static async Task<FreeSOLotInfo[]> GetAllOnline()
        {
            using (var data = await freeSOStatusClient.GetAsync("/userapi/city/1/lots/online?compact=false"))
            {
                JArray jarr = ((dynamic)JsonConvert.DeserializeObject(await data.Content.ReadAsStringAsync())).lots;
                var retobj = jarr.ToObject<FreeSOLotInfo[]>();
                retobj[0].success = data.IsSuccessStatusCode;
                return retobj;
            }
        }

        public static async Task<FreeSOLotInfo> TryGetOnlineLot(string name)
        {
            var retobj = await GetAllOnline();
            try
            {
                var info = retobj.First(x => x.name.ToLower() == name.ToLower());
                info.lot_thumbnailurl = GetLotThumbnail(info.location);
                info.success = retobj[0].success;
                info.isOnline = true;
                return info;
            }
            catch (Exception)
            {
                return new FreeSOLotInfo();
            }
        }

        public static async Task<FreeSOLotInfo> GetLotByName(string name)
        {
            var onlineInfo = await TryGetOnlineLot(name);
            uint avatars_in_lot = 0;
            if (onlineInfo.isOnline)
                avatars_in_lot = onlineInfo.avatars_in_lot;
            using (var data = await freeSOStatusClient.GetAsync("/userapi/city/1/lots/name/" + name))
            {
                var info = JsonConvert.DeserializeObject<FreeSOLotInfo>(await data.Content.ReadAsStringAsync(), new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                info.lot_thumbnailurl = GetLotThumbnail(info.location);
                info.success = data.IsSuccessStatusCode;
                info.isOnline = onlineInfo.isOnline;
                info.avatars_in_lot = avatars_in_lot;
                return info;
            }
        }
    }
}
