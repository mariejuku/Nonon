using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Rest;
using Newtonsoft.Json;

namespace Nonon {
    class CommandsDriver {
        Program program; //reference to the bot
        Storer storer; //reference to the bot's storer
        Statistics data; //reference to bot's data
        SocketTextChannel log; //logging channel
        SocketGuild guild;
        DiscordSocketClient client;
        public StatsDriver stats;

        public CommandsDriver(Program p, Storer s, SocketTextChannel l, Statistics d, SocketGuild g, DiscordSocketClient c) {
            program = p; log = l; storer = s; data = d; guild = g; client = c;
        }

        private async Task Say(SocketTextChannel log, string message) {
            //shortcut
            program.Say(log, message);
        }

        private async Task DirectSay(SocketUser user, string message) {
            //shortcut
            program.DirectSay(user, message);
        }

        public void Parse(SocketMessage message) {
            //intercept and understand a command and then take the appropriate action.
            StringEater command = new StringEater(message.Content.ToLower());
            command.Get(); //remove the mention
            if (command.Find("help")) {
                Say(message.Channel as SocketTextChannel, message.Author.Mention + ", I sent you the help list.");
                Say(message.Channel as SocketTextChannel, message.Author.Mention + "Keep in mind I'm still kinda WIP and things may break.");
                string s = "Here are my commands.\n" +
                    "They work in a logical way. Ask for an item to see the counts of that item on the server.\n" +
                    "Item can be users, channels, reacts, or mentions.\n" +
                    "add 'in <channel name>' to an item command to see just the items occurring in that channel.\n" +
                    "add 'by <user name>' to an item command to see just the items produced by that user.\n\n" +
                    "**help**\t\tView this list.\n" +
                    "**me**\t\tShows your stats.\n" +
                    "~~<item>\t\tShows stats for this kind of item.~~\n" +
                    "**top <item>**\t\tShows the top 10 of the item.\n" +
                    "\t\t\tYou may also use 'in' and 'by' keywords for this command.\n" +
                    "~~user <username>\t\tShows the stats for this user.~~\n" +
                    "~~channel <channelname>\t\tShows the stats for this channel.~~\n" +
                    "~~server\t\tShows the stats for the server.~~\n" +
                    "~~friendships\t\tShows the biggest friendships on the server.~~\n";
                DirectSay(message.Author, s);
            } else if (command.Find("me")) {
                Statistics.User me = null;
                string say = ""; string phrase = ""; int i = 0;
                foreach (var user in data.users) {
                    if (user.id == message.Author.Id) {
                        me = user;
                        say = " Here are your stats:\n";
                        say += "You've sent " + user.messagesSent + " messages during your time here.\n";
                        say += "You've also posted " + user.reactions.Count + " reactions.\n";
                        //find busiest channel
                        Statistics.Channel bc = null;
                        int bcm = 0;
                        foreach (var channel in data.channels) {
                            foreach (var containedUser in channel.containedUsers) {
                                if (containedUser.id == message.Author.Id) {
                                    if (containedUser.messagesSent > bcm) {
                                        bcm = containedUser.messagesSent;
                                        bc = channel;
                                    }
                                }
                            }
                        }
                        if (bc != null) {
                            var channel = guild.GetChannel(bc.id);
                            float percent = (bc.messageCount / bcm);
                            say += "Your busiest channel is " + channel.Name + ", and you have sent " + bcm + " messages there.\n" +
                                "That's " + percent + "% of that channel's discussion.\n";
                        }

                        //find favourite reaction
                        Dictionary<string, int> reactionsDict = new Dictionary<string, int>();
                        foreach (var reaction in user.reactions) {
                            if (!reactionsDict.ContainsKey(reaction.emotename)) {
                                reactionsDict.Add(reaction.emotename, 0);
                            }
                            reactionsDict[reaction.emotename]++;
                        }
                        if (reactionsDict.Count != 0) {
                            //sort by frequency
                            var reactionsList = reactionsDict.ToList();
                            reactionsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
                            if (reactionsList.Count != 0) {
                                foreach (var reaction in reactionsList) {
                                    i++; if (i > 20) { break; }
                                    phrase += i + "\t\t" + reaction.Key + "\t\tused " + reaction.Value + " times\n";
                                }
                                say += "\nYour top " + (i - 1) + " favourite reactions are:\n" + phrase;
                            }
                        }
                        //find favourite mention
                        Dictionary<ulong, int> mentionsDict = new Dictionary<ulong, int>();
                        foreach (var mention in user.mentions) {
                            if (!mentionsDict.ContainsKey(mention.mentionedId)) {
                                mentionsDict.Add(mention.mentionedId, 0);
                            }
                            mentionsDict[mention.mentionedId]++;
                        }
                        if (mentionsDict.Count != 0) {
                            i = 0; phrase = "";
                            //sort by frequency
                            var mentionsList = mentionsDict.ToList();
                            mentionsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
                            if (mentionsList.Count != 0) {
                                foreach (var mention in mentionsList) {
                                    var guser = guild.GetUser(mention.Key);
                                    if (guser != null) {
                                        i++; if (i > 10) { break; }
                                        phrase += i + "\t\t" + program.getBestName(guser) + "\t\tmentioned " + mention.Value + " times\n";
                                    }
                                }
                                say += "\nYour " + (i - 1) + " best friends are:\n" + phrase;
                            }
                        }
                    }
                }
                if (me == null) {
                    say = "Sorry, I don't have any information about you.";
                }
                Say(message.Channel as SocketTextChannel, message.Author.Mention + say);
            } else if (command.Find("top")) {
                string selectedItem = null;
                string query = null;
                int i = 0;
                string say = "";
                string sayChunk = "";
                bool doAdd = true;
                SocketUser byUser = null;
                SocketTextChannel inChannel = null;
                if (command.Find("channels")) {
                    selectedItem = "channel";
                } else if (command.Find("users")) {
                    selectedItem = "user";
                } else if (command.Find("mentions")) {
                    selectedItem = "mention";
                } else if (command.Find("reacts") || command.Find("reactions")) {
                    selectedItem = "reaction";
                }
                if (command.Find("by")) {
                    query = command.Get();
                    //they specified a user so let's go find it
                    foreach (var user in guild.Users) {
                        if (user.Username == query || user.Nickname == query || user.Mention == query) {
                            Console.WriteLine("Located that user.");
                            byUser = user;
                        }
                    }
                }
                if (command.Find("in")) {
                    query = command.Get();
                    //they specified a channel so let's go find it
                    foreach (var channel in guild.TextChannels) {
                        if (channel.Mention == query || channel.Name == query) {
                            Console.WriteLine("Located that channel.");
                            inChannel = channel;
                        }
                    }
                }
                switch (selectedItem) {
                    case "channel":
                        Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + stats.TopChannels(guild, data));
                        //Console.WriteLine(message.Author.Mention + " " + statistics.TopChannels(guild, data));
                        break;
                    case "user":
                        Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + stats.TopUsers(guild, data, inChannel));
                        //Console.WriteLine(message.Author.Mention + " " + statistics.TopUsers(guild, data, inChannel));
                        break;
                    case "mention":
                        Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + stats.TopMentions(guild, data, inChannel, byUser));
                        //Console.WriteLine(message.Author.Mention + " " + statistics.TopMentions(guild, data, inChannel, byUser));
                        break;
                    case "reaction":
                        Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + stats.TopReactions(guild, data, inChannel, byUser));
                        //Console.WriteLine(message.Author.Mention + " " + statistics.TopReactions(guild, data, inChannel, byUser));
                        break;
                    default:
                        say = "Sorry friend, but I don't understand that sequence.";
                        break;
                }
                //await Say(message.Channel as SocketTextChannel, say);
            } else if (command.Find("channels")) {
            }
        }
        
    }
}