// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.IO;
using System.Threading.Tasks;
using Discord.Webhook;

namespace Hybrasyl.Plugins
{
    namespace Hybrasyl.Plugins
    {
        /// <summary>
        ///     A message handler plugin
        /// </summary>
        public class EventTranscriber : MessagePlugin, IProcessingMessageHandler
        {
            private DiscordWebhookClient client;
            private string OutputDir = string.Empty;
            private string WebhookUrl = string.Empty;

            public override bool Initialize(IHandlerConfiguration config)
            {
                config.TryGetValue("WebhookUrl", out var WebhookUrl);

                if (!string.IsNullOrEmpty(WebhookUrl))
                    client = new DiscordWebhookClient(WebhookUrl);

                if (config.TryGetValue("OutputDir", out var dir))
                {
                    OutputDir = Path.Join(Game.DataDirectory, dir);
                    Disabled = false;
                    return true;
                }

                throw new ArgumentException("Initialize: OutputDir must be defined, aborting");
            }

            public IMessagePluginResponse Process(Message inbound)
            {
                var resp = new MessagePluginResponse();

                if (Disabled)
                {
                    resp.Success = false;
                    resp.PluginResponse =
                        "Sorry, the bug reporter is currently disabled. We apologize for the inconvenience.";
                    return resp;
                }

                var id = Random.Shared.RandomString(8);

                // Transmit message to discord, also save locally

                var now = DateTime.Now;
                var text =
                    $"**Bug Report Submission**\n\n**Bug ID**: {id}\n**From**: {inbound.Sender}\n**Date**: {now.ToString()}\n\n**Subject**: {inbound.Subject}";

                if (inbound.Text.Length > 1800)
                    text = $"{text}\n\n{inbound.Text.Substring(0, 1800)} ...\n(Truncated. Full message on server)";
                else
                    text = $"{text}\n\n{inbound.Text}";

                Task.Run(function: () => client.SendMessageAsync(text));
                Task.Run(action: () => SaveToDisk(inbound.Sender, id, text));
                resp.Success = true;
                resp.PluginResponse = $"Thank you for your bug submission (BUG-{id}). It has been received.";
                return resp;
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
        }
    }
}