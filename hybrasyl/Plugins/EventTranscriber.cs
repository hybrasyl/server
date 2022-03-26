namespace Hybrasyl.Plugins
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Discord.Webhook;

    namespace Hybrasyl.Plugins
    {
        /// <summary>
        /// A message handler plugin 
        /// </summary>
        public class EventTranscriber : MessagePlugin, IProcessingMessageHandler
        {
            private string WebhookUrl = string.Empty;
            private string OutputDir = string.Empty;

            private DiscordWebhookClient client;

            public override bool Initialize(IHandlerConfiguration config)
            {
                config.TryGetValue("WebhookUrl", out string WebhookUrl);

                if (!string.IsNullOrEmpty(WebhookUrl))
                    client = new DiscordWebhookClient(WebhookUrl);

                if (config.TryGetValue("OutputDir", out string dir))
                {
                    OutputDir = Path.Join(global::Hybrasyl.Game.StartupDirectory, dir);
                    Disabled = false;
                    return true;
                }
                else
                    throw new ArgumentException("Initialize: OutputDir must be defined, aborting");
            }

            private async void SaveToDisk(string sender, string id, string text)
            {
                try
                {
                    await File.WriteAllTextAsync(Path.Join(OutputDir, $"bugreport-{sender}-{id}.txt"), text);
                }
                catch (Exception e)
                {
                    GameLog.Error("BugReporter: failure to write out log: {e}, plugin disabled", e);
                    Disabled = true;
                }
            }
            public IMessagePluginResponse Process(Message inbound)
            {
                var resp = new MessagePluginResponse();

                if (Disabled)
                {
                    resp.Success = false;
                    resp.PluginResponse = "Sorry, the bug reporter is currently disabled. We apologize for the inconvenience.";
                    return resp;
                }

                var id = Random.Shared.RandomString(8);

                // Transmit message to discord, also save locally

                var now = DateTime.Now;
                string text = $"**Bug Report Submission**\n\n**Bug ID**: {id}\n**From**: {inbound.Sender}\n**Date**: {now.ToString()}\n\n**Subject**: {inbound.Subject}";

                if (inbound.Text.Length > 1800)
                    text = $"{text}\n\n{inbound.Text.Substring(0, 1800)} ...\n(Truncated. Full message on server)";
                else
                    text = $"{text}\n\n{inbound.Text}";

                Task.Run(() => client.SendMessageAsync(text));
                Task.Run(() => SaveToDisk(inbound.Sender, id, text));
                resp.Success = true;
                resp.PluginResponse = $"Thank you for your bug submission (BUG-{id}). It has been received.";
                return resp;
            }
        }
    }

}
