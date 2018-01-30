using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
{
    public class ChatCommandHandler
    {
        private Dictionary<string, (Type Type, List<int> argCount)> _associates = new Dictionary<string, (Type, List<int>)>();
        private static readonly ILog UserLogger = LogManager.GetLogger("UserActivityLogger");
        private static readonly ILog GmLogger = LogManager.GetLogger("GmActivityLogger");
        public static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Regex QuotesRegex = new Regex(" (?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
        private static readonly Regex ArgsRegex = new Regex("(\\[[a-zA-Z\\<\\> ]*\\])");

        public ChatCommandHandler()
        {
            foreach (var x in typeof(ChatCommand).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ChatCommand))))
            {
                try
                {
                    // ArgumentText example
                    // <string foo> <int bar> | <int baz> [<string quux> <int bazbar>] 
                    string command = (string)x.GetField("Command", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    var argtext = (string)x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    var options = argtext.Split('|');
                    var allowedArgcounts = new List<int>();
                        
                    foreach (var option in options)
                    {
                        // Count number of required / optional arguments
                        var split = ArgsRegex.Split(option);
                        if (split.Count() == 1)
                        {
                            allowedArgcounts.Add(option.Count(e => e == '<'));
                        }
                        else
                        {
                            // Now get the bracket contents

                            foreach (Group group in ArgsRegex.Match(argtext).Groups)
                            {
                                var baseopt = argtext.Remove(argtext.IndexOf(group.Value)).Count(e => e == '<');
                                allowedArgcounts.Add(baseopt + group.Value.Count(e => e == '<'));
                                allowedArgcounts.Add(baseopt);
                            }
                        }

                    }
                    // int argcount = ((string)x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static).GetValue(null)).Count(e => e == '<');
                    _associates.Add(command, (x, allowedArgcounts));
                    Logger.Info($"{command} registered");
                }
                catch (Exception e)
                {
                    Logger.Warn($"{x.Name}: could not be loaded - {e}");
                }
            }
        }

        public void IsHandler(string command) => _associates.ContainsKey(command);

        public bool TryGetHandler(string command, out Type handler)
        {
            handler = null;
            if (_associates.ContainsKey(command))
            {
                handler = _associates[command].Type;
                return true;
            }
            return false;
        }

        public void Handle(User user, string command, string args)
        {
            if (_associates.ContainsKey(command))
            {
                var strings = QuotesRegex.Matches(args).Cast<Match>().Select(m => m.Value).ToArray();

                var handler = _associates[command];
                var priv = (bool) handler.Type.GetField("Privileged", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                if (priv && !user.IsPrivileged)
                {
                    user.SendSystemMessage("Failed: Access denied (command is privileged)");
                    UserLogger.Error($"{user.Name}: denied attempt to use privileged command {command}");
                    return;
                }

                var splitArgs = QuotesRegex.Split(args).Select(e => e.Replace("\"", "")).ToArray();

                if (splitArgs.Length == 1 && string.IsNullOrEmpty(splitArgs[0]))
                    splitArgs = new string[0];

                if (!handler.argCount.Contains(splitArgs.Length))
                {
                    var argText = (string) handler.Type.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    user.SendSystemMessage($"Usage: {command} {argText}");
                    return;
                }

                if (user.IsPrivileged)
                {
                    var type = (priv == true ? "privileged" : "unprivileged");
                    GmLogger.Warn($"{user.Name}: executing {type} command {command} {args}");
                }
                else
                    UserLogger.Info($"{user.Name}: executing command {command} {args}");


                var wtf = handler.Type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

                var result = (ChatCommandResult) wtf.Invoke(null, new object[] { user, splitArgs });

                user.SendMessage(result.Message, result.MessageType);
            }
            else { user.SendSystemMessage("No such command, try /help."); }
        }
    }
}
