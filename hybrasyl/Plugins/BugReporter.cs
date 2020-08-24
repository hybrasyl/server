using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;

namespace Hybrasyl.Plugins
{
    /// <summary>
    /// A message handler plugin 
    /// </summary>
    public class BugReporter : IMessageHandler
    {
        private static Random rand = new Random();
        private string WebhookUrl = string.Empty;
        private string OutputDir = string.Empty;
        private DiscordWebhookClient client;
        public bool Disabled { get; set; }

        public bool Initialize(IHandlerConfiguration config)
        {
            if (config.TryGetValue("WebhookUrl", out string url) && config.TryGetValue("OutputDir", out string dir))
            {
                WebhookUrl = url;
                OutputDir = Path.Join(Constants.DataDirectory, dir);
                client = new DiscordWebhookClient(url);
                Disabled = false;
                return true;
            }
            else
                throw new ArgumentException("Initialize: needed WebhookUrl and OutputDir to be defined, aborting");
        }

        public async void Process(Message inbound)
        {
            if (Disabled)
                return;

            var id = rand.RandomString(8);

            // Transmit message to discord, also save locally

            string text = $"**Bug Report Submission**\n\n**Bug ID**: {id}\n\n **From**: {inbound.Sender}\n\n**Subject**: {inbound.Subject}";

            if (inbound.Text.Length > 1800)
                text = $"{text}\n\n{inbound.Text.Substring(0, 1800)} ...\n(Truncated. Full message on server)";
            else
                text = $"{text}\n\n{inbound.Text}";


            try
            {
                var now = DateTime.Now;
                await client.SendMessageAsync(inbound.Text);
                await File.WriteAllTextAsync(Path.Join(OutputDir, $"bugreport-{inbound.Sender}-{id}.txt"), text);
            }
            catch (Exception e)
            {
                GameLog.Error("BugReporter plugin: disabled, exception occurred writing message: {e}", e);
                Disabled = true;
            }
        }
    }
}
