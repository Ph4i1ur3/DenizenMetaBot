using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Abstract base class for commands that users can run.
    /// </summary>
    public abstract class UserCommands
    {
        /// <summary>
        /// Prefix for when the bot successfully handles user input.
        /// </summary>
        public const string SUCCESS_PREFIX = "+++ ";

        /// <summary>
        /// Prefix for when the bot refuses user input.
        /// </summary>
        public const string REFUSAL_PREFIX = "--- ";

        /// <summary>
        /// The backing meta bot instance.
        /// </summary>
        public DenizenMetaBot Bot;

        /// <summary>
        /// Sends a reply to a message in the same channel.
        /// </summary>
        /// <param name="message">The message to reply to.</param>
        /// <param name="embed">The embed message to send.</param>
        public static void SendReply(SocketMessage message, Embed embed)
        {
            message.Channel.SendMessageAsync(embed: embed).Wait();
        }

        /// <summary>
        /// Sends a generic positive reply to a message in the same channel.
        /// </summary>
        public static void SendGenericPositiveMessageReply(SocketMessage message, string title, string description)
        {
            SendReply(message, GetGenericPositiveMessageEmbed(title, description));
        }

        /// <summary>
        /// Sends a generic negative reply to a message in the same channel.
        /// </summary>
        public static void SendGenericNegativeMessageReply(SocketMessage message, string title, string description)
        {
            SendReply(message, GetGenericNegativeMessageEmbed(title, description));
        }

        /// <summary>
        /// Sends an error message reply to a message in the same channel.
        /// </summary>
        public static void SendErrorMessageReply(SocketMessage message, string title, string description)
        {
            SendReply(message, GetErrorMessageEmbed(title, description));
        }

        /// <summary>
        /// Creates an Embed object for a generic positive message.
        /// </summary>
        public static Embed GetGenericPositiveMessageEmbed(string title, string description)
        {
            return new EmbedBuilder().WithTitle(title).WithColor(0, 255, 255).WithDescription(description).Build();
        }

        /// <summary>
        /// Creates an Embed object for a generic negative message.
        /// </summary>
        public static Embed GetGenericNegativeMessageEmbed(string title, string description)
        {
            return new EmbedBuilder().WithTitle(title).WithColor(255, 128, 0).WithDescription(description).Build();
        }

        /// <summary>
        /// Creates an Embed object for an error message.
        /// </summary>
        public static Embed GetErrorMessageEmbed(string title, string description)
        {
            return new EmbedBuilder().WithTitle(title).WithColor(255, 64, 32).WithThumbnailUrl(Constants.WARNING_ICON).WithDescription(description).Build();
        }

        /// <summary>
        /// Escapes user input for output. Best when wrapped in `backticks`.
        /// </summary>
        /// <param name="text">The user input text.</param>
        /// <returns>The escaped result.</returns>
        public static string EscapeUserInput(string text)
        {
            return text.Replace('`', '\'');
        }
    }
}
