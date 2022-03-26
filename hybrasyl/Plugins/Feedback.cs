using System;
using System.IO;
using System.Threading.Tasks;
using Discord.Webhook;

namespace Hybrasyl.Plugins;

/// <summary>
/// A message handler plugin 
/// </summary>
public class FeedbackReporter : MessagePlugin, IProcessingMessageHandler
{
    private string WebhookUrl = string.Empty;
    private string OutputDir = string.Empty;
       
    private DiscordWebhookClient client;

    public override bool Initialize(IHandlerConfiguration config)
    {
        if (config.TryGetValue("WebhookUrl", out string url) && config.TryGetValue("OutputDir", out string dir))
        {
            WebhookUrl = url;
            OutputDir = Path.Join(Game.StartupDirectory, dir);
            client = new DiscordWebhookClient(url);
            Disabled = false;
            return true;
        }
        else
            throw new ArgumentException("Initialize: needed WebhookUrl and OutputDir to be defined, aborting");
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
        string text = $"**Feedback Submission**\n\n**ID**: {id}\n**From**: {inbound.Sender}\n**Date**: {now.ToString()}\n\n**Subject**: {inbound.Subject}";

        if (inbound.Text.Length > 1800)
            text = $"{text}\n\n{inbound.Text.Substring(0, 1800)} ...\n(Truncated. Full message on server)";
        else
            text = $"{text}\n\n{inbound.Text}";

        Task.Run(() => client.SendMessageAsync(text));
        Task.Run(() => SaveToDisk(inbound.Sender, id, text));
        resp.Success = true;
        resp.PluginResponse = $"Thank you for your feedback (FEED-{id}). It has been logged.";
        return resp;
    }
}