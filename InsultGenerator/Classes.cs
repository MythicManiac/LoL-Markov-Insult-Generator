using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InsultGenerator
{
    public class MessageCollection
    {
        public List<Message> Messages { get; private set; }
        private ChampionAliasMap _aliases;

        public MessageCollection()
        {
            Messages = new List<Message>();
            _aliases = ChampionAliasMap.Load("champion-aliases.json");
        }

        public void FilterByChampions(string[] champions, bool allowEmpty)
        {
            var result = new List<Message>();
            foreach(var message in Messages)
            {
                if (!message.IsAllReferencesContainedIn(champions, allowEmpty))
                {
                    continue;
                }
                result.Add(message);
            }
            Messages = result;
        }

        public void FilterByToxicity(bool isToxic)
        {
            var result = new List<Message>();
            foreach(var message in Messages)
            {
                if (message.IsToxic == isToxic) result.Add(message);
            }
            Messages = result;
        }

        public void AddMessage(Message message)
        {
            Messages.Add(message);
        }

        public void AddMessage(MessageJsonEntry message, HashSet<string> champions)
        {
            var convertedMessage = new Message();
            convertedMessage.IsAllChat = message.sent_to.ToLower() == "all";
            convertedMessage.IsAlly = message.association_to_offender.ToLower() == "ally";
            convertedMessage.IsToxic = message.association_to_offender.ToLower() == "offender";
            convertedMessage.Content = message.message;
            convertedMessage.FindMentions(champions, _aliases);
            Messages.Add(convertedMessage);
        }
    }

    public class Champion
    {
        public string Name { get; set; }
        public Champion(string name) { Name = name; }
        public override string ToString(){ return Name; }
    }

    public class ChampionAliasMap
    {
        private Dictionary<Champion, string[]> _championToAlias;
        private Dictionary<string, Champion> _nameToChampion;

        private ChampionAliasMap()
        {
            _championToAlias = new Dictionary<Champion, string[]>();
            _nameToChampion = new Dictionary<string, Champion>();
        }

        private Champion GerOrAddChampion(string champion)
        {
            if (!_nameToChampion.ContainsKey(champion)) _nameToChampion.Add(champion, new Champion(champion));
            return _nameToChampion[champion];
        }

        private void AddByChampion(string champion, string[] aliases)
        {
            var championObject = GerOrAddChampion(champion);
            _championToAlias.Add(championObject, aliases);
        }

        public string[] GetByChampion(string champion)
        {
            return _championToAlias[_nameToChampion[champion]];
        }

        public static ChampionAliasMap Load(string filePath)
        {
            var result = new ChampionAliasMap();
            var aliases = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(File.ReadAllText(filePath));
            foreach (var entry in aliases)
            {
                result.AddByChampion(entry.Key, entry.Value);
            }
            return result;
        }
    }

    public class MessageJsonEntry
    {
        public string date;
        public string time;
        public string summoner_name;
        public string sent_to;
        public string message;
        public string association_to_offender;
        public string champion_name;
        public int name_change;
    }

    public class Message
    {
        public bool IsToxic { get; set; }
        public bool IsAllChat { get; set; }
        public bool IsAlly { get; set; }
        public string Content { get; set; }
        public List<string> ReferredChampions { get; set; }

        public Message()
        {
            ReferredChampions = new List<string>();
        }

        public override string ToString()
        {
            return Content;
        }

        private void AddReferredChampion(string name)
        {
            if (ReferredChampions.Contains(name)) return;
            ReferredChampions.Add(name);
        }

        public void FindMentions(HashSet<string> champions, ChampionAliasMap championAliases)
        {
            foreach (var champion in champions)
            {
                var aliases = championAliases.GetByChampion(champion);
                foreach (var alias in aliases)
                {
                    var message = Content.ToLower();
                    if (message.Contains(alias)) AddReferredChampion(champion);
                }
            }
        }

        public bool IsAllReferencesContainedIn(string[] champions, bool allowEmpty)
        {
            var lowercaseChampions = new string[champions.Length];
            for (var i = 0; i < champions.Length; i++)
            {
                lowercaseChampions[i] = champions[i].ToLower();
            }

            if (ReferredChampions.Count == 0) return allowEmpty;
            foreach (var champion in ReferredChampions)
            {
                if (!lowercaseChampions.Contains(champion.ToLower())) return false;
            }
            return true;
        }
    }
}
