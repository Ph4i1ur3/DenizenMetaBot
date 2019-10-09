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
using FreneticUtilities.FreneticToolkit;
using DenizenBot.MetaObjects;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to look up meta documentation.
    /// </summary>
    public class MetaCommands : UserCommands
    {
        /// <summary>
        /// Checks whether meta commands are denied in the relevant channel. If denied, will return 'true' and show a rejection message.
        /// </summary>
        /// <param name="message">The message being replied to.</param>
        /// <returns>True if they are denied.</returns>
        public bool CheckMetaDenied(SocketMessage message)
        {
            if (!Bot.MetaCommandsAllowed(message.Channel))
            {
                SendErrorMessageReply(message, "Command Not Allowed Here",
                    "Meta documentation commands are not allowed in this channel. Please switch to a bot spam channel, or a Denizen channel.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Automatically processes a meta search command.
        /// </summary>
        /// <typeparam name="T">The meta object type.</typeparam>
        /// <param name="docs">The docs mapping.</param>
        /// <param name="type">The meta type.</param>
        /// <param name="cmds">The command args.</param>
        /// <param name="message">The Discord message object.</param>
        /// <param name="secondarySearch">A secondary search string if the first fails.</param>
        /// <param name="secondaryMatcher">A secondary matching function if needed.</param>
        /// <param name="altSingleOutput">An alternate method of processing the single-item-result.</param>
        /// <param name="altFindClosest">Alternate method to find the closest result.</param>
        /// <returns>How close of an answer was gotten (0 = perfect, -1 = no match needed, 1000 = none).</returns>
        public int AutoMetaCommand<T>(Dictionary<string, T> docs, MetaType type, string[] cmds, SocketMessage message,
            string secondarySearch = null, Func<T, bool> secondaryMatcher = null, Action<T> altSingleOutput = null,
            Func<string> altFindClosest = null, Func<List<T>, List<T>> altMatchOrderer = null) where T: MetaObject
        {
            if (CheckMetaDenied(message))
            {
                return -1;
            }
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, $"Need input for '{type.Name}' command",
                    $"Please specify a {type.Name} to search, like `!{type.Name} Some{type.Name}Here`. Or, use `!{type.Name} all` to view all documented {type.Name.ToLowerFast()}s.");
                return -1;
            }
            string search = cmds[0].ToLowerFast();
            if (search == "all")
            {
                SendGenericPositiveMessageReply(message, $"All {type.Name}", $"Find all {type.Name} at {Constants.DOCS_URL_BASE}{type.WebPath}/");
                return -1;
            }
            if (altSingleOutput == null)
            {
                altSingleOutput = (singleObj) => SendReply(message, singleObj.GetEmbed().Build());
            }
            if (altFindClosest == null)
            {
                altFindClosest = () => StringConversionHelper.FindClosestString(docs.Keys, search, 20);
            }
            if (altMatchOrderer == null)
            {
                altMatchOrderer = (list) => list.OrderBy((mat) => StringConversionHelper.GetLevenshteinDistance(search, mat.CleanName)).ToList();
            }
            if (!docs.TryGetValue(search, out T obj) && (secondarySearch == null || !docs.TryGetValue(secondarySearch, out obj)))
            {
                List<T> matched = new List<T>();
                List<T> strongMatched = new List<T>();
                foreach (KeyValuePair<string, T> objPair in docs)
                {
                    if (objPair.Key.Contains(search) || (secondarySearch != null && objPair.Key.Contains(secondarySearch)))
                    {
                        strongMatched.Add(objPair.Value);
                    }
                    if (secondaryMatcher != null && secondaryMatcher(objPair.Value))
                    {
                        matched.Add(objPair.Value);
                    }
                }
                if (strongMatched.Count > 0)
                {
                    matched = strongMatched;
                }
                if (matched.Count == 0)
                {
                    string closeName = altFindClosest();
                    SendErrorMessageReply(message, $"Cannot Find Searched {type.Name}", $"Unknown {type.Name.ToLowerFast()}." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                    return closeName == null ? 1000 : StringConversionHelper.GetLevenshteinDistance(search, closeName);
                }
                else if (matched.Count > 1)
                {
                    matched = altMatchOrderer(matched);
                    string suffix = ".";
                    if (matched.Count > 20)
                    {
                        matched = matched.GetRange(0, 20);
                        suffix = ", ...";
                    }
                    string listText = string.Join("`, `", matched);
                    SendErrorMessageReply(message, $"Cannot Specify Searched {type.Name}", $"Multiple possible {type.Name.ToLowerFast()}s: `{listText}`{suffix}");
                    return StringConversionHelper.GetLevenshteinDistance(search, matched[0].CleanName);
                }
                else // Count == 1
                {
                    obj = matched[0];
                    Console.WriteLine($"Meta-Command for '{type.Name}' found imperfect single match for search '{search}': '{obj.CleanName}'");
                    altSingleOutput(obj);
                    return StringConversionHelper.GetLevenshteinDistance(search, matched[0].CleanName);
                }
            }
            Console.WriteLine($"Meta-Command for '{type.Name}' found perfect match for search '{search}': '{obj.CleanName}'");
            altSingleOutput(obj);
            return 0;
        }

        /// <summary>
        /// Command meta docs user command.
        /// </summary>
        public void CMD_Command(string[] cmds, SocketMessage message)
        {
            void singleReply(MetaCommand cmd)
            {
                if (cmds.Length >= 2)
                {
                    string outputType = cmds[1].ToLowerFast();
                    if (outputType.StartsWith("u"))
                    {
                        SendReply(message, cmd.GetUsagesEmbed().Build());
                    }
                    else if (outputType.StartsWith("t"))
                    {
                        SendReply(message, cmd.GetTagsEmbed().Build());
                    }
                    else
                    {
                        SendErrorMessageReply(message, "Bad Command Syntax", "Second argument is unknown.\n\nUsage: `command [name] [usage/tags]`.");
                    }
                }
                else
                {
                    SendReply(message, cmd.GetEmbed().Build());
                }
            }
            int closeness = AutoMetaCommand(Program.CurrentMeta.Commands, MetaDocs.META_TYPE_COMMAND, cmds, message, altSingleOutput: singleReply);
            if (closeness > 0)
            {
                string closeMech = StringConversionHelper.FindClosestString(Program.CurrentMeta.Mechanisms.Keys.Select(s => s.After('.')), cmds[0].ToLowerFast(), 10);
                if (closeMech != null)
                {
                    SendGenericPositiveMessageReply(message, "Possible Confusion", $"Did you mean to search for `mechanism {closeMech}`?");
                }
            }
        }

        /// <summary>
        /// Mechanism meta docs user command.
        /// </summary>
        public void CMD_Mechanism(string[] cmds, SocketMessage message)
        {
            string secondarySearch = null;
            if (cmds.Length > 0)
            {
                int dotIndex = cmds[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    secondarySearch = cmds[0].Substring(0, dotIndex) + "tag" + cmds[0].Substring(dotIndex);
                }
            }
            int closeness = AutoMetaCommand(Program.CurrentMeta.Mechanisms, MetaDocs.META_TYPE_MECHANISM, cmds, message, secondarySearch);
            if (closeness > 0)
            {
                string closeCmd = StringConversionHelper.FindClosestString(Program.CurrentMeta.Commands.Keys, cmds[0].ToLowerFast(), 7);
                if (closeCmd != null)
                {
                    SendGenericPositiveMessageReply(message, "Possible Confusion", $"Did you mean to search for `command {closeCmd}`?");
                }
            }
        }

        /// <summary>
        /// Tag meta docs user command.
        /// </summary>
        public void CMD_Tag(string[] cmds, SocketMessage message)
        {
            string secondarySearch = null;
            if (cmds.Length > 0)
            {
                cmds[0] = MetaTag.CleanTag(cmds[0]);
                int dotIndex = cmds[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    secondarySearch = cmds[0].Substring(0, dotIndex) + "tag" + cmds[0].Substring(dotIndex);
                }
            }
            int getDistanceTo(MetaTag tag)
            {
                int dist1 = StringConversionHelper.GetLevenshteinDistance(cmds[0], tag.CleanedName);
                int dist2 = StringConversionHelper.GetLevenshteinDistance(cmds[0], tag.AfterDotCleaned);
                int dist3 = secondarySearch == null ? int.MaxValue : StringConversionHelper.GetLevenshteinDistance(secondarySearch, tag.CleanedName);
                return Math.Min(Math.Min(dist1, dist2), dist3);
            }
            string findClosestTag()
            {
                int lowestDistance = 20;
                string lowestStr = null;
                foreach (MetaTag tag in Program.CurrentMeta.Tags.Values)
                {
                    int currentDistance = getDistanceTo(tag);
                    if (currentDistance < lowestDistance)
                    {
                        lowestDistance = currentDistance;
                        lowestStr = tag.CleanedName;
                    }
                }
                return lowestStr;
            }
            AutoMetaCommand(Program.CurrentMeta.Tags, MetaDocs.META_TYPE_TAG, cmds, message, secondarySearch, altFindClosest: findClosestTag,
                altMatchOrderer: (list) => list.OrderBy(getDistanceTo).ToList());
        }

        /// <summary>
        /// Event meta docs user command.
        /// </summary>
        public void CMD_Event(string[] cmds, SocketMessage message)
        {
            string onSearch = string.Join(" ", cmds).ToLowerFast();
            string secondarySearch = onSearch.StartsWith("on ") ? onSearch.Substring("on ".Length) : onSearch;
            onSearch = "on " + secondarySearch;
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            AutoMetaCommand(Program.CurrentMeta.Events, MetaDocs.META_TYPE_EVENT, cmds, message, null,
            (e) => e.RegexMatcher.IsMatch(onSearch));
        }

        /// <summary>
        /// Action meta docs user command.
        /// </summary>
        public void CMD_Action(string[] cmds, SocketMessage message)
        {
            string secondarySearch = string.Join(" ", cmds).ToLowerFast();
            secondarySearch = secondarySearch.StartsWith("on ") ? secondarySearch.Substring("on ".Length) : secondarySearch;
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            AutoMetaCommand(Program.CurrentMeta.Actions, MetaDocs.META_TYPE_ACTION, cmds, message);
        }

        /// <summary>
        /// Language meta docs user command.
        /// </summary>
        public void CMD_Language(string[] cmds, SocketMessage message)
        {
            string secondarySearch = string.Join(" ", cmds).ToLowerFast();
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            AutoMetaCommand(Program.CurrentMeta.Languages, MetaDocs.META_TYPE_LANGUAGE, cmds, message);
        }

        /// <summary>
        /// Meta docs total search command.
        /// </summary>
        public void CMD_Search(string[] cmds, SocketMessage message)
        {
            if (CheckMetaDenied(message))
            {
                return;
            }
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Need input for Search command", "Please specify some text to search, like `!search someobjecthere`.");
                return;
            }
            for (int i = 0; i < cmds.Length; i++)
            {
                cmds[i] = cmds[i].ToLowerFast();
            }
            string fullSearch = string.Join(' ', cmds);
            List<MetaObject> strongMatch = new List<MetaObject>();
            List<MetaObject> partialStrongMatch = new List<MetaObject>();
            List<MetaObject> weakMatch = new List<MetaObject>();
            List<MetaObject> partialWeakMatch = new List<MetaObject>();
            foreach (MetaObject obj in Program.CurrentMeta.AllMetaObjects())
            {
                if (obj.CleanName.Contains(fullSearch))
                {
                    strongMatch.Add(obj);
                    continue;
                }
                foreach (string word in cmds)
                {
                    if (obj.CleanName.Contains(word))
                    {
                        partialStrongMatch.Add(obj);
                        goto fullContinue;
                    }
                }
                if (obj.Searchable.Contains(fullSearch))
                {
                    weakMatch.Add(obj);
                    continue;
                }
                if (fullSearch.Contains(obj.CleanName))
                {
                    partialWeakMatch.Add(obj);
                    continue;
                }
                foreach (string word in cmds)
                {
                    if (obj.Searchable.Contains(word))
                    {
                        partialWeakMatch.Add(obj);
                        goto fullContinue;
                    }
                }
            fullContinue:
                continue;
            }
            if (strongMatch.IsEmpty() && partialStrongMatch.IsEmpty() && weakMatch.IsEmpty() && partialWeakMatch.IsEmpty())
            {
                SendErrorMessageReply(message, "Search Command Has No Results", "Input search text could not be found.");
                return;
            }
            string suffix = ".";
            void listWrangle(string typeShort, string typeLong, List<MetaObject> objs)
            {
                objs = objs.OrderBy((obj) => StringConversionHelper.GetLevenshteinDistance(fullSearch, obj.CleanName)).ToList();
                suffix = ".";
                if (objs.Count > 20)
                {
                    objs = objs.GetRange(0, 20);
                    suffix = ", ...";
                }
                string listText = string.Join("`, `", objs.Select((obj) => $"!{obj.Type.Name} {obj.CleanName}"));
                SendGenericPositiveMessageReply(message, $"{typeShort} Search Results", $"{typeShort} ({typeLong}) search results: `{listText}`{suffix}");
            }
            if (strongMatch.Any())
            {
                listWrangle("Best", "very close", strongMatch);
            }
            if (partialStrongMatch.Any())
            {
                listWrangle("Probable", "close but imperfect", partialStrongMatch);
                if (strongMatch.Any())
                {
                    return;
                }
            }
            if (weakMatch.Any())
            {
                listWrangle("Possible", "might be related", weakMatch);
                if (strongMatch.Any() || partialStrongMatch.Any())
                {
                    return;
                }
            }
            if (partialWeakMatch.Any())
            {
                listWrangle("Weak", "if nothing else, some chance of being related", partialWeakMatch);
            }
        }
    }
}
