/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Enums;
using Hybrasyl.Plugins;

namespace Hybrasyl.Messaging;

internal static class MessagingController
{
    public static ServerPacketStructures.MessagingResponse UnknownError =>
        new()
        {
            ResponseString = "An unknown error occurred.",
            ResponseType = BoardResponseType.EndResult,
            ResponseSuccess = false
        };

    public static ServerPacketStructures.MessagingResponse BoardList(GuidReference userRef)
    {
        var boards = new List<(ushort Id, string Name)>();

        // "Mail" is hardcoded in the client and does not need to be sent here
        // We also store a special mailbox called sent messages, which has all of the 
        // user's stored messages

        boards.Add((ushort.MaxValue - 1, $"{userRef.UserName}'s Sent Messages"));

        foreach (var board in Game.World.WorldState.Values<Board>().Where(predicate: mb => mb.Global &&
                     mb.CheckAccessLevel(userRef.UserName,
                         BoardAccessLevel.Read)))
            boards.Add(((ushort) board.Id, board.DisplayName));

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = BoardResponseType.DisplayList,
            Boards = boards
        };
    }

    public static ServerPacketStructures.MessagingResponse GetMessageList(GuidReference userRef, ushort boardId,
        short startPostId, bool isClick = false)
    {
        MessageStore store;
        var displayname = string.Empty;
        var responseType = BoardResponseType.GetBoardIndex;
        if (boardId == 0)
        {
            store = Game.World.WorldState.GetOrCreate<Mailbox>(userRef);
            displayname = $"{store.DisplayName}'s Mail";
            responseType = BoardResponseType.GetMailboxIndex;
        }
        else if (boardId == ushort.MaxValue - 1)
        {
            store = Game.World.WorldState.GetOrCreate<SentMail>(userRef);
            displayname = $"{userRef.UserName}'s Sent Messages";
        }
        else
        {
            if (Game.World.WorldState.TryGetValueByIndex(boardId, out Board board))
            {
                store = board;
                displayname = board.DisplayName;
            }
            else
            {
                return new ServerPacketStructures.MessagingResponse
                {
                    ResponseString = "Board not found.",
                    ResponseType = BoardResponseType.EndResult,
                    ResponseSuccess = false
                };
            }
        }

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = responseType,
            isClick = isClick,
            BoardId = boardId,
            BoardName = displayname,
            Messages = store.GetIndex()
        };
    }

    public static ServerPacketStructures.MessagingResponse GetMessage(GuidReference userRef, short postId, sbyte offset,
        ushort boardId)
    {
        Message message = null;
        var error = "An unknown error occured.";
        MessageStore store;
        if (boardId == 0)
        {
            store = Game.World.WorldState.GetOrCreate<Mailbox>(userRef);
        }
        else if (boardId == ushort.MaxValue - 1)
        {
            store = Game.World.WorldState.GetOrCreate<SentMail>(userRef);
        }
        else
        {
            if (Game.World.WorldState.TryGetValueByIndex(boardId, out Board board))
            {
                if (!board.CheckAccessLevel(userRef.UserName, BoardAccessLevel.Read))
                    return new ServerPacketStructures.MessagingResponse
                    {
                        ResponseType = BoardResponseType.EndResult,
                        ResponseString = "Access denied.",
                        ResponseSuccess = false
                    };
                store = board;
            }
            else
            {
                return new ServerPacketStructures.MessagingResponse
                {
                    ResponseType = BoardResponseType.EndResult,
                    ResponseString = "That message store was not found."
                };
            }
        }

        var messageId = postId - 1;
        switch (offset)
        {
            case 0:
            {
                // postId is the exact message
                if (postId >= 0 && postId <= store.Messages.Count)
                    message = store.Messages[messageId];
                else
                    error = "That post could not be found.";
                break;
            }
            case 1:
            {
                // Client clicked "prev", which hilariously means "newer"
                // postId in this case is the next message
                if (postId > store.Messages.Count)
                {
                    error = "There are no newer messages.";
                }
                else
                {
                    var messageList = store.Messages.GetRange(messageId,
                        store.Messages.Count - messageId);
                    message = messageList.Find(match: m => m.Deleted == false);

                    if (message == null)
                        error = "There are no newer messages.";
                }
            }
                break;
            case -1:
            {
                // Client clicked "next", which means "older"
                // postId is previous message
                if (postId < 0)
                {
                    error = "There are no older messages.";
                }
                else
                {
                    var messageList = store.Messages.GetRange(0, postId);
                    messageList.Reverse();
                    message = messageList.Find(match: m => m.Deleted == false);
                    if (message == null)
                        error = "There are no older messages.";
                }
            }
                break;

            default:
            {
                error = "Invalid offset (nice try, chief)";
            }
                break;
        }

        if (message != null)
        {
            message.Read = true;
            //                    Mailbox.Save();
            //                    user.UpdateAttributes(StatUpdateFlags.Secondary);
            return new ServerPacketStructures.MessagingResponse
            {
                BoardId = 0,
                Messages = message.InfoAsList,
                ResponseType = store is Mailbox ? BoardResponseType.GetMailMessage : BoardResponseType.GetBoardMessage
            };
        }

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = BoardResponseType.EndResult,
            ResponseSuccess = false,
            ResponseString = error
        };
    }

    public static ServerPacketStructures.MessagingResponse DeleteMessage(GuidReference userRef, ushort boardId,
        ushort postId)
    {
        var response = string.Empty;
        var success = false;
        var messageId = postId - 1;

        if (boardId == 0 || boardId == ushort.MaxValue - 1) // Mailbox access
        {
            MessageStore store;
            if (boardId == 0)
                store = Game.World.WorldState.GetOrCreate<Mailbox>(userRef);
            else
                store = Game.World.WorldState.GetOrCreate<SentMail>(userRef);

            if (store.DeleteMessage(messageId))
            {
                response = "The message was destroyed.";
                success = true;
            }
            else
            {
                response = "The message could not be found.";
            }
        }
        else if (Game.World.WorldState.TryGetValueByIndex(boardId, out Board board))
        {
            if (Game.World.WorldState.TryGetAuthInfo(userRef.UserName, out var ainfo))
            {
                var delmsg = board.GetMessage(messageId);

                if (delmsg == null)
                {
                    response = "That message could not be found.";
                }

                else if (ainfo.IsPrivileged || board.CheckAccessLevel(ainfo.Username, BoardAccessLevel.Moderate) ||
                         delmsg.Sender.ToLower() == userRef.UserName.ToLower())
                {
                    if (board.DeleteMessage(postId - 1))
                    {
                        response = "The message was destroyed.";
                        success = true;
                    }
                    else
                    {
                        response = "Sorry, an error occurred.";
                    }
                }
                else
                {
                    response = "You can't do that.";
                }
            }
            else
            {
                response = "Authentication information could not be verified.";
            }
        }
        else
        {
            response = "Board not found.";
        }

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = BoardResponseType.DeleteMessage,
            ResponseSuccess = success,
            ResponseString = response
        };
    }

    public static ServerPacketStructures.MessagingResponse SendMessage(GuidReference senderRef, ushort boardId,
        string recipient, string subject, string body)
    {
        var response = string.Empty;
        var success = true;

        var senderSentMail = Game.World.WorldState.GetOrCreate<SentMail>(senderRef);

        // Don't allow blank title or subject
        if (string.IsNullOrWhiteSpace(subject))
        {
            success = false;
            response = "The message had no subject.";
        }
        else if (string.IsNullOrWhiteSpace(body))
        {
            success = false;
            response = "You can't send empty messages.";
        }
        else if (!body.IsAscii() || !subject.IsAscii())
        {
            success = false;
            response = "You can't use special characters in a message (ASCII only).";
        }
        else if (boardId == ushort.MaxValue - 1)
        {
            success = false;
            response = "You can't post messages here.";
        }

        // Handle plugin response

        if (!Game.World.TryGetActiveUser(senderRef.UserName, out _))
            // Currently a user must be online to send mail, so if we get here, something really wacky is happening
            return UnknownError;
        try
        {
            IMessageHandler handler;
            Xml.Objects.MessageType type;
            type = boardId == 0 ? Xml.Objects.MessageType.Mail : Xml.Objects.MessageType.BoardMessage;

            var message = new Plugins.Message(type, senderRef.UserName, recipient, subject, body);

            handler = Game.World.ResolveMessagingPlugin(type, message);

            if (handler is IProcessingMessageHandler pmh && success)
            {
                var msg = new Plugins.Message(Xml.Objects.MessageType.Mail, senderRef.UserName, recipient, subject, body);
                var resp = pmh.Process(msg);
                if (!pmh.Passthrough)
                {
                    // TODO: implement cast / resolve duplication
                    var hmsg = new Message(recipient, senderRef.UserName, subject, body);
                    senderSentMail.ReceiveMessage(hmsg);

                    // Plugin is "last destination" for message
                    return new ServerPacketStructures.MessagingResponse
                    {
                        ResponseType = BoardResponseType.EndResult,
                        ResponseSuccess = resp.Success,
                        ResponseString = resp.PluginResponse
                    };
                }

                if (resp.Transformed)
                {
                    // Update message if transformed, and keep going
                    recipient = resp.Message.Recipient;
                    subject = resp.Message.Subject;
                    body = resp.Message.Text;
                }
            }
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            success = false;
            response = "An unknown error occurred. Sorry!";
        }

        // Annoyingly board replies use the same packet path as sending mail replies, so we need to handle
        // both here
        if (boardId == 0 && success)
        {
            var receiverRef = Game.World.WorldState.GetGuidReference(recipient);
            if (receiverRef == null)
            {
                success = false;
                response = "Sadly, no record of that person exists in the realm.";
            }
            else
            {
                var mailbox = Game.World.WorldState.GetOrCreate<Mailbox>(receiverRef);
                var msg = new Message(recipient, senderRef.UserName, subject, body);
                try
                {
                    if ((DateTime.Now - senderSentMail.LastMailMessageSent).TotalSeconds <
                        Constants.MAIL_MESSAGE_COOLDOWN &&
                        senderSentMail.LastMailRecipient == recipient)
                    {
                        success = false;
                        response = $"You've sent too much mail to {recipient} recently. Give it a rest.";
                    }
                    else if (mailbox.ReceiveMessage(msg))
                    {
                        response = $"Your letter to {recipient} was sent.";
                        GameLog.InfoFormat("mail: {0} sent message to {1}", senderRef.UserName, recipient);
                        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.MailNotifyUser,
                            recipient));
                        senderSentMail.LastMailRecipient = recipient;
                        senderSentMail.LastMailMessageSent = DateTime.Now;
                    }
                    else
                    {
                        success = false;
                        response =
                            $"{recipient}'s mailbox is full or locked. A copy was kept in your sent mailbox. Sorry!";
                    }
                }
                catch (MessageStoreLocked e)
                {
                    Game.ReportException(e);
                    success = false;
                    response = $"{recipient} cannot receive mail at this time. Sorry!";
                }

                senderSentMail.ReceiveMessage((Message) msg.Clone());
            }
        }
        else if (success)
        {
            if (Game.World.WorldState.TryGetValueByIndex(boardId, out Board board))
            {
                if (Game.World.WorldState.TryGetAuthInfo(senderRef.UserName, out var ainfo))
                {
                    if (ainfo.IsPrivileged || board.CheckAccessLevel(ainfo.Username, BoardAccessLevel.Write))
                    {
                        var msg = new Message(board.DisplayName, senderRef.UserName, subject, body);
                        if (board.ReceiveMessage(msg))
                        {
                            response = "The message was sent.";
                            success = true;
                            var sentMsg = (Message) msg.Clone();
                            sentMsg.Recipient = board.DisplayName;
                            senderSentMail.ReceiveMessage(sentMsg);
                        }
                        else
                        {
                            response = "The message could not be sent.";
                        }
                    }
                    else
                    {
                        response = "You don't have permission to do that.";
                    }
                }
                else
                {
                    response = "Authentication information could not be verified.";
                }
            }
            else
            {
                response = "Board not found.";
            }
        }

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = BoardResponseType.EndResult,
            ResponseString = response,
            ResponseSuccess = success
        };
    }

    public static ServerPacketStructures.MessagingResponse HighlightMessage(GuidReference userRef, ushort boardId,
        short postId)
    {
        var response = string.Empty;
        var success = false;
        Board board;
        var messageId = postId - 1;

        if (Game.World.WorldState.TryGetAuthInfo(userRef.UserName, out var ainfo) && ainfo.IsPrivileged)
        {
            if (Game.World.WorldState.TryGetValueByIndex(boardId, out board))
            {
                if (board.ToggleHighlight((short) messageId))
                {
                    response = "The message was highlighted.";
                    success = true;
                }
                else
                {
                    response = "Message not found.";
                }
            }
            else
            {
                response = "Board not found.";
            }
        }
        else
        {
            response = "You cannot highlight this message.";
        }

        return new ServerPacketStructures.MessagingResponse
        {
            ResponseType = BoardResponseType.HighlightMessage,
            ResponseString = response,
            ResponseSuccess = success
        };
    }
}