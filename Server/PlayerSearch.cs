using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using WebSocketSharp;

namespace hypixel
{
    public class PlayerSearch
    {
        public static PlayerSearch Instance;

        public static Dictionary<string, HashSet<PlayerResult>> players = new Dictionary<string, HashSet<PlayerResult>>();

        private static ConcurrentDictionary<string, int> nameRequests = new ConcurrentDictionary<string, int>();

        ConcurrentDictionary<string, int> playerHits = new ConcurrentDictionary<string, int>();

        static PlayerSearch()
        {
            Instance = new PlayerSearch();
            FileController.CreatePath("players/");
        }

        internal string GetName(string uuid)
        {
            using(var context = new HypixelContext())
            {
                return context.Players
                    .Where(player => player.UuId == uuid)
                    .Select(player => player.Name)
                    .FirstOrDefault();
            }
        }

        public static void ClearCache()
        {
            players.Clear();
        }

        /// <summary>
        /// Registers that a specific player was looked up and modifies the search order acordingly
        /// </summary>
        /// <param name="name"></param>
        public void AddHitFor(string uuid)
        {
            playerHits.AddOrUpdate(uuid, 1, (key, value) => value + 1);
        }

        public void SaveHits(HypixelContext context)
        {
            var hits = playerHits;
            playerHits = new ConcurrentDictionary<string, int>();
            foreach (var hit in hits)
            {
                var player = context.Players.Where(player => player.UuId == hit.Key).First();
                player.HitCount += hit.Value;
                context.Update(player);
            }
        }

        public void SaveNameForPlayer(string name, string uuid)
        {
            //Console.WriteLine($"Saving {name} ({uuid})");
            var index = name.Substring(0, 3).ToLower();
            string path = "players/" + index;
            lock(path)
            {
                HashSet<PlayerResult> list = null;
                if (FileController.Exists(path))
                    list = FileController.LoadAs<HashSet<PlayerResult>>(path);
                if (list == null)
                    list = new HashSet<PlayerResult>();
                list.Add(new PlayerResult(name, uuid));
                FileController.SaveAs(path, list);
            }
        }

        public async void LoadName(User user)
        {
            if (!user.Name.IsNullOrEmpty() && user.Name.Length > 2)
            {

                // already loaded
                return;
            }

            lock(nameRequests)
            {
                if (nameRequests.ContainsKey(user.uuid))
                {
                    // already on its way
                    return;
                }
                nameRequests[user.uuid] = 1;
            }

            var name = await Program.GetPlayerNameFromUuid(user.uuid);

            if (name.IsNullOrEmpty())
            {
                //Console.WriteLine($"\rCould not get name for: {user.uuid} {user.Name}");
                return;
            }

            user.Name = name;
            SaveNameForPlayer(name, user.uuid);
        }

        public async Task<IEnumerable<PlayerResult>> Search(string search, int count, bool forceResolution = true)
        {
            if (count <= 0 || search.Contains(' '))
                return new PlayerResult[0];

            List<PlayerResult> result;

            using(var context = new HypixelContext())
            {

                result = await context.Players
                    .Where(e => EF.Functions.Like(e.Name, $"{search.Replace("_","\\_")}%"))
                    .OrderBy(p => p.Name.Length - p.HitCount - (p.Name == search ? 10000000 : 0))
                    .Select(p => new PlayerResult(p.Name, p.UuId, p.HitCount))
                    .Take(count)
                    .ToListAsync();

                if (result.Count() == 0)
                {
                    var client = new RestClient("https://mc-heads.net/");
                    var request = new RestRequest($"/minecraft/profile/{search}", Method.GET);
                    var response = await client.ExecuteAsync(request);
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        if (forceResolution)
                            throw new CoflnetException("player_not_found", $"we don't know of a player with the name {search}");
                        else
                        { // nothing to do 
                        }
                    else
                    {
                        var value = JsonConvert.DeserializeObject<SearchCommand.MinecraftProfile>(response.Content);
                        NameUpdater.UpdateUUid(value.Id, value.Name);
                        result.Add(new PlayerResult(value.Name, value.Id));
                    }

                }

            }
            return result;
        }
    }
}