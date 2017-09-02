using InsultGenerator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TribunalParser
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var program = new Program();
            program.Run();
        }

        private MessageCollection _messages;

        public Program()
        {
            _messages = new MessageCollection();
        }

        public void Run()
        {
            var sources = FindAllSourcesAt("cases");
            var progress = 0;
            foreach(var source in sources)
            {
                ExportMessages(source);
                progress++;
                Console.Write("\rProgress: {0:00.00} %", (float)progress / sources.Length * 100);
            }
            Console.WriteLine();
            var messagesJson = JsonConvert.SerializeObject(_messages, Formatting.Indented);
            File.WriteAllText("input-data.json", messagesJson);
            Console.WriteLine("Done!");
            Console.ReadKey();
        }

        public string[] FindAllSourcesAt(string location)
        {
            var files = Directory.GetFiles(location);
            var result = new List<string>(files.Length);
            foreach (var file in files)
            {
                if (file.Split('.').Last() == "txt")
                {
                    result.Add(file);
                }
            }
            return result.ToArray();
        }

        public void ExportMessages(string filepath)
        {
            var content = JObject.Parse(File.ReadAllText(filepath));
            var messageLog = (JArray)content.SelectToken("chat_log");

            var champions = new HashSet<string>();
            foreach(JObject message in messageLog)
            {
                var deserializedMessage = message.ToObject<MessageJsonEntry>();
                if (deserializedMessage.champion_name == null) continue;
                if (champions.Contains(deserializedMessage.champion_name)) continue;
                champions.Add(deserializedMessage.champion_name);
            }

            foreach(JObject message in messageLog)
            {
                var deserializedMessage = message.ToObject<MessageJsonEntry>();
                _messages.AddMessage(deserializedMessage, champions);
            }
        }
    }
}
