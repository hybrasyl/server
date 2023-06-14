using System;
using System.IO;
using System.Threading.Tasks;
using Discord.Webhook;

namespace Hybrasyl.Plugins;

/// <summary>
///     A message handler plugin
/// </summary>
public class FeedbackReporter : MessagePlugin, IProcessingMessageHandler
{
    private DiscordWebhookClient client;
    private string OutputDir = string.Empty;
    private string WebhookUrl = string.Empty;

    public override bool Initialize(IHandlerConfiguration config)
    {
        if (config.TryGetValue("WebhookUrl", out var url) && config.TryGetValue("OutputDir", out var dir))
        {
            WebhookUrl = url;
            OutputDir = Path.Join(Game.DataDirectory, dir);
            client = new DiscordWebhookClient(url);
            Disabled = false;
            return true;
        }

        throw new ArgumentException("Initialize: needed WebhookUrl and OutputDir to be defined, aborting");
    }

    public IMessagePluginResponse Process(Message inbound)
    {
        var resp = new MessagePluginResponse();

        if (Disabled)
        {
            resp.Success = false;
            resp.PluginResponse = "Sorry, feedback is currently disabled. We apologize for the inconvenience.";
            return resp;
        }

        var id = Random.Shared.RandomString(8);

        // Transmit message to discord, also save locally

        var now = DateTime.Now;
        var text =
            $"**Feedback Submission**\n\n**ID**: {id}\n**From**: {inbound.Sender}\n**Date**: {now.ToString()}\n\n**Subject**: {inbound.Subject}";

        if (inbound.Text.Length > 1800)
            text = $"{text}\n\n{inbound.Text.Substring(0, 1800)} ...\n(Truncated. Full message on server)";
        else
            text = $"{text}\n\n{inbound.Text}";

        Task.Run(function: () => client.SendMessageAsync(text));
        Task.Run(action: () => SaveToDisk(inbound.Sender, id, text));
        resp.Success = true;
        resp.PluginResponse = $"Thank you for your feedback (FEED-{id}). It has been logged.";
        return resp;
    }

    private async void SaveToDisk(string sender, string id, string text)
    {
        try
        {
            await File.WriteAllTextAsync(Path.Join(OutputDir, $"feedback-{sender}-{id}.txt"), text);
        }
        catch (Exception e)
        {
            GameLog.Error("Feedback: failure to write out log: {e}, plugin disabled", e);
            Disabled = true;
        }
    }
}