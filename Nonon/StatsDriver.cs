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
    class StatsDriver {
        Program program; //reference to the bot
        Storer storer; //reference to the bot's stats storer
        Statistics data; //reference to bot's data
        SocketTextChannel log; //logging channel
        SocketGuild guild;
        DiscordSocketClient client;

        public StatsDriver(Program p, Storer s, SocketTextChannel l, Statistics d, SocketGuild g, DiscordSocketClient c) {
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

        public async Task ScanServer(DiscordSocketClient client, SocketGuild guild) {
            bool verbose = true;
            ulong total = 0;
            Console.WriteLine("Performing server history scan");
            await client.SetGameAsync("Tallying message history...");
            if (verbose) { await Say(log, "I'm scanning the message history! I'm heckin verbose about it today too"); }
            var channels = guild.TextChannels;
            Console.WriteLine("Found " + channels.Count + " channels");
            foreach (var channel in channels) {
                await client.SetGameAsync("Tallying message history in #" + channel.Name);
                Console.WriteLine("Tallying message history in #" + channel.Name + "... (" + total + " messages seen so far)");
                if (verbose) { await Say(log, "Tallying message history in #" + channel.Name + "... (" + total + " messages seen so far)"); }
                var messages = channel.GetMessagesAsync(1).Flatten();
                var startMessage = messages.FirstOrDefault();
                Console.WriteLine("Start message is " + startMessage.Id);
                /*if (startMessage != null) {
                    try {
                        messages = channel.GetMessagesAsync((ulong)startMessage.Id, Direction.Before, 100).Flatten();
                        
                        while (await messages.Count() != 0) {
                            foreach (var message in messages) {
                                //Console.WriteLine("Downloaded message: " + message.Channel.Name + " >> " + message.Content + " @ " + message.CreatedAt.ToString("g"));
                                total++;
                                try {
                                    ParsePastMessage(channel as SocketGuildChannel, message);
                                } catch(Exception e) {
                                    Console.WriteLine(e.Message + "\n\n" + e.StackTrace + "\n\n\n");
                                    Console.WriteLine(message.Id);
                                }
                            }
                            try {
                                messages = await channel.GetMessagesAsync(messages.LastOrDefault(), Direction.Before, 100).Flatten();
                            } catch (Exception e) {
                                Console.WriteLine(e.Message + "\n\n" + e.StackTrace + "\n\n\n");
                            }
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e.Message + "\n\n" + e.StackTrace + "\n\n\n");
                    }
                }*/
            }
            //save stats
            storer.save(data);
            //return Task.CompletedTask;
        }

        private void ParsePastMessage(SocketGuildChannel guildChannel, IMessage message) {
            string[] messageWords = message.Content.Split(null);
            //record stats ------------------------------------------------------------------------------------
            //update stats on the user
            Statistics.User dataUser = data.FindUser(message.Author);
            dataUser.messagesSent += 1;
            //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.messagesSent + " messages.");
            //update stats on the channel
            Statistics.Channel dataChannel = data.FindChannel(message.Channel);
            dataChannel.messageCount += 1;
            Statistics.User dataChannelUser = dataChannel.FindUser(message.Author);
            dataChannelUser.messagesSent += 1;
            //Console.WriteLine("User " + dataChannelUser.id + " has sent " + dataChannelUser.messagesSent + " messages on this channel.");
            //check for mentions
            foreach (var mentionedUser in message.MentionedUserIds) {
                var socketGuildUserFromId = guildChannel.GetUser(mentionedUser);
                data.AddMention(socketGuildUserFromId);
                dataUser.AddMention(socketGuildUserFromId);
                dataChannel.AddMention(socketGuildUserFromId);
                dataChannelUser.AddMention(socketGuildUserFromId);
            }

            //check for parsed mentions
            SocketUser suser;
            SocketGuildUser guser;
            bool mentioned;
            foreach (string word in messageWords) {
                foreach (Statistics.User user in data.users) {
                    suser = client.GetUser(user.id);
                    guser = guild.GetUser(user.id);
                    mentioned = false;
                    if (suser != null && suser.Username.ToLower() == word.ToLower()) { mentioned = true; }
                    if (guser != null && program.getBestName(guser).ToLower() == word.ToLower()) { mentioned = true; }
                    if (mentioned) {
                        data.AddMention(suser);
                        dataUser.AddMention(suser);
                        dataChannel.AddMention(suser);
                        dataChannelUser.AddMention(suser);
                    }
                }
            }

            //todo: check for reactions
        }

        public void ParseMessage(SocketGuildChannel guildChannel, SocketMessage message) {
            string[] messageWords = message.Content.Split(null);
            //record stats ------------------------------------------------------------------------------------
            //update stats on the user
            Statistics.User dataUser = data.FindUser(message.Author);
            dataUser.messagesSent += 1;
            //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.messagesSent + " messages.");
            //update stats on the channel
            Statistics.Channel dataChannel = data.FindChannel(message.Channel);
            dataChannel.messageCount += 1;
            Statistics.User dataChannelUser = dataChannel.FindUser(message.Author);
            dataChannelUser.messagesSent += 1;
            //Console.WriteLine("User " + dataChannelUser.id + " has sent " + dataChannelUser.messagesSent + " messages on this channel.");
            //check for mentions
            foreach (SocketUser mentionedUser in message.MentionedUsers) {
                data.AddMention(mentionedUser);
                dataUser.AddMention(mentionedUser);
                dataChannel.AddMention(mentionedUser);
                dataChannelUser.AddMention(mentionedUser);
            }

            //check for parsed mentions
            SocketUser suser;
            SocketGuildUser guser;
            bool mentioned;
            foreach (string word in messageWords) {
                foreach (Statistics.User user in data.users) {
                    suser = client.GetUser(user.id);
                    guser = guild.GetUser(user.id);
                    mentioned = false;
                    if (suser != null && suser.Username.ToLower() == word.ToLower()) { mentioned = true; }
                    if (guser != null && program.getBestName(guser).ToLower() == word.ToLower()) { mentioned = true; }
                    if (mentioned) {
                        data.AddMention(suser);
                        dataUser.AddMention(suser);
                        dataChannel.AddMention(suser);
                        dataChannelUser.AddMention(suser);
                    }
                }
            }
        }
        public string TopChannels(SocketGuild guild, Statistics data) {
            Dictionary<ulong, int> channels = new Dictionary<ulong, int>();
            foreach (var channel in data.channels) {
                channels.Add(channel.id, channel.messageCount);
            }
            //sort by frequency
            var channelsList = channels.ToList();
            channelsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            //print
            int i = 0; string say = "";
            foreach (var channel in channelsList) {
                i++; if (i > 10) { break; }
                say += i + "\t\t" + guild.GetChannel(channel.Key).Name + "\t\twith " + channel.Value + " messages\n";
            }
            say = "The top " + (i - 1) + " most used channels are:\n" + say;
            return say;
        }

        public string TopUsers(SocketGuild guild, Statistics data, SocketTextChannel filterChannel) {
            //describe stats on users
            //return dictionary of matching users
            Dictionary<ulong, int> users = new Dictionary<ulong, int>(); //dictionary is users and message count
                                                                         //get data objects
            Statistics.Channel filterDataChannel = null;
            if (filterChannel != null) {
                foreach (var channel in data.channels) {
                    if (channel.id == filterChannel.Id) {
                        filterDataChannel = channel;
                    }
                }
            }
            if (filterChannel == null) {
                //server's users
                foreach (var user in data.users) {
                    users.Add(user.id, user.messagesSent);
                }
            } else {
                //channel's users
                foreach (var user in filterDataChannel.containedUsers) {
                    users.Add(user.id, user.messagesSent);
                }
            }
            //sort by frequency
            var usersList = users.ToList();
            usersList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            //print
            int i = 0; string say = "";
            if (usersList.Count != 0) {
                if (filterChannel == null) {
                    foreach (var user in usersList) {
                        SocketGuildUser guser = guild.GetUser(user.Key);
                        if (guser != null) {
                            i++; if (i > 10) { break; }
                            say += i + "\t\t" + program.getBestName(guser) + "\t\thas sent " + user.Value + " messages\n";
                        }
                    }
                    say = "The top " + (i - 1) + " most verbose users on " + guild.Name + " are:\n" + say;
                } else {
                    foreach (var user in usersList) {
                        SocketGuildUser guser = guild.GetUser(user.Key);
                        if (guser != null) {
                            i++; if (i > 10) { break; }
                            say += i + "\t\t" + program.getBestName(guild.GetUser(user.Key)) + "\t\thas sent " + user.Value + " messages\n";
                        }
                    }
                    say = "The top " + (i - 1) + " most active denizens of " + filterChannel.Name + " are:\n" + say;
                }
            } else {
                say = "sorry, it Looks like no users have spoken there yet!";
            }
            return say;
        }

        public string TopMentions(SocketGuild guild, Statistics data, SocketTextChannel filterChannel, SocketUser filterUser) {
            //describe stats on mentions
            //return dictionary of matching mentions
            Console.WriteLine("Getting mentions.");
            Console.WriteLine("Filter channel is " + (filterChannel == null ? "null" : filterChannel.Id.ToString()));
            Console.WriteLine("Filter user is " + (filterUser == null ? "null" : filterUser.Id.ToString()));

            Dictionary<ulong, int> mentions = new Dictionary<ulong, int>(); //dictionary is users mentioned by frequency
                                                                            //get data objects
            Statistics.User filterDataUser = null;
            Statistics.Channel filterDataChannel = null;
            if (filterUser != null) {
                foreach (var user in data.users) {
                    if (user.id == filterUser.Id) {
                        filterDataUser = user;
                    }
                }
            }
            if (filterChannel != null) {
                foreach (var channel in data.channels) {
                    if (channel.id == filterChannel.Id) {
                        filterDataChannel = channel;
                    }
                }
            }
            if (filterChannel == null && filterUser == null) {
                //server's mentions
                foreach (var mention in data.mentions) {
                    if (!mentions.ContainsKey(mention.mentionedId)) {
                        mentions.Add(mention.mentionedId, 0);
                    }
                    mentions[mention.mentionedId]++;
                }
            } else if (filterChannel == null) {
                //user's mentions
                foreach (var mention in filterDataUser.mentions) {
                    if (!mentions.ContainsKey(mention.mentionedId)) {
                        mentions.Add(mention.mentionedId, 0);
                    }
                    mentions[mention.mentionedId]++;
                }
            } else if (filterUser == null) {
                //channels's mentions
                foreach (var mention in filterDataChannel.containedMentions) {
                    if (!mentions.ContainsKey(mention.mentionedId)) {
                        mentions.Add(mention.mentionedId, 0);
                    }
                    mentions[mention.mentionedId]++;
                }
            } else {
                //filter by both
                foreach (var channel in data.channels) {
                    if (channel.id == filterChannel.Id) {
                        foreach (var user in channel.containedUsers) {
                            if (user.id == filterUser.Id) {
                                foreach (var mention in user.mentions) {
                                    if (!mentions.ContainsKey(mention.mentionedId)) {
                                        mentions.Add(mention.mentionedId, 0);
                                    }
                                    mentions[mention.mentionedId]++;
                                }
                            }
                        }
                    }
                }
            }
            //sort by frequency
            var mentionsList = mentions.ToList();
            mentionsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            //print
            int i = 0; string say = "";
            if (mentionsList.Count != 0) {
                if (filterChannel == null && filterUser == null) {
                    foreach (var mention in mentionsList) {
                        var guser = guild.GetUser(mention.Key);
                        if (guser != null) {
                            if (i > 10) { break; }
                            i++;
                            say += i + "\t\t" + program.getBestName(guser) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                    }
                    say = "The top " + (i - 1) + " most popular users on " + guild.Name + " are:\n" + say;
                } else if (filterChannel == null) {
                    foreach (var mention in mentionsList) {
                        var guser = guild.GetUser(mention.Key);
                        if (guser != null) {
                            if (i > 10) { break; }
                            i++;
                            say += i + "\t\t" + program.getBestName(guser) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                    }
                    say = program.getBestName(filterUser) + "'s " + (i - 1) + " best friends are:\n" + say;
                } else if (filterUser == null) {
                    foreach (var mention in mentionsList) {
                        var guser = guild.GetUser(mention.Key);
                        if (guser != null) {
                            if (i > 10) { break; }
                            i++;
                            say += i + "\t\t" + program.getBestName(guser) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                    }
                    say = "The top " + (i - 1) + " most popular users in " + filterChannel.Name + " are:\n" + say;
                } else {
                    foreach (var mention in mentionsList) {
                        var guser = guild.GetUser(mention.Key);
                        if (guser != null) {
                            if (i > 10) { break; }
                            i++;
                            say += i + "\t\t" + program.getBestName(guser) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                    }
                    say = program.getBestName(filterUser) + "'s " + (i - 1) + " favourite people when chatting in " + filterChannel.Name + " are:\n" + say;
                }
            } else {
                say = "sorry, it Looks like no mentions have occurred yet!";
            }
            return say;
        }

        public string TopReactions(SocketGuild guild, Statistics data, SocketTextChannel filterChannel, SocketUser filterUser) {
            //describe stats on reactions
            Console.WriteLine("Getting mentions.");
            Console.WriteLine("Filter channel is " + (filterChannel == null ? "null" : filterChannel.Id.ToString()));
            Console.WriteLine("Filter user is " + (filterUser == null ? "null" : filterUser.Id.ToString()));

            Dictionary<string, int> reactions = new Dictionary<string, int>(); //dictionary is reactions by frequency
            Dictionary<ulong, int> users = new Dictionary<ulong, int>(); //dictionary is users by reaction count
                                                                         //get data objects
            Statistics.User filterDataUser = null;
            Statistics.Channel filterDataChannel = null;
            if (filterUser != null) {
                foreach (var user in data.users) {
                    if (user.id == filterUser.Id) {
                        filterDataUser = user;
                    }
                }
            }
            if (filterChannel != null) {
                foreach (var channel in data.channels) {
                    if (channel.id == filterChannel.Id) {
                        filterDataChannel = channel;
                    }
                }
            }
            if (filterChannel == null && filterUser == null) {
                //server's reactions
                foreach (var reaction in data.reactions) {
                    if (!reactions.ContainsKey(reaction.emotename)) {
                        reactions.Add(reaction.emotename, 0);
                    }
                    reactions[reaction.emotename]++;
                }
                foreach (var user in data.users) {
                    if (!users.ContainsKey(user.id)) {
                        users.Add(user.id, user.reactions.Count);
                    }
                }
            } else if (filterChannel == null) {
                //user's reactions
                foreach (var reaction in filterDataUser.reactions) {
                    if (!reactions.ContainsKey(reaction.emotename)) {
                        reactions.Add(reaction.emotename, 0);
                    }
                    reactions[reaction.emotename]++;
                }
            } else if (filterUser == null) {
                //channels's reactions
                foreach (var reaction in filterDataChannel.containedReactions) {
                    if (!reactions.ContainsKey(reaction.emotename)) {
                        reactions.Add(reaction.emotename, 0);
                    }
                    reactions[reaction.emotename]++;
                }
            } else {
                //filter by both
                foreach (var channel in data.channels) {
                    if (channel.id == filterChannel.Id) {
                        foreach (var user in channel.containedUsers) {
                            if (user.id == filterUser.Id) {
                                foreach (var reaction in user.reactions) {
                                    if (!reactions.ContainsKey(reaction.emotename)) {
                                        reactions.Add(reaction.emotename, 0);
                                    }
                                    reactions[reaction.emotename]++;
                                }
                            }
                        }
                    }
                }
            }
            //sort by frequency
            var reactionsList = reactions.ToList();
            reactionsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            var usersList = users.ToList();
            usersList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            //print
            int i = 0; string say = ""; string phrase = "";
            if (reactionsList.Count != 0) {
                foreach (var reaction in reactionsList) {
                    i++; if (i > 20) { break; }
                    say += i + "\t\t" + reaction.Key + "\t\tused " + reaction.Value + " times\n";
                }
                if (filterChannel == null && filterUser == null) {
                    say = "The top " + (i - 1) + " most popular reactions on " + guild.Name + " are:\n" + say;
                    i = 0;
                    if (usersList.Count != 0) {
                        foreach (var user in usersList) {
                            var guser = guild.GetUser(user.Key);
                            if (guser != null) {
                                i++; if (i > 10) { break; }
                                phrase += i + "\t\t" + guser + "\t\thas posted " + user.Value + " reactions\n";
                            }
                        }
                        say += "\n" + guild.Name + "'s " + i + " biggest reactors are:\n" + phrase;
                    }
                } else if (filterChannel == null) {
                    say = program.getBestName(filterUser) + "'s " + (i - 1) + " favourite reactions are:\n" + say;
                } else if (filterUser == null) {
                    say = "The top " + (i - 1) + " most popular reactions used in " + filterChannel.Name + " are:\n" + say;
                } else {
                    say = program.getBestName(filterUser) + "'s " + (i - 1) + " favourite reactions when chatting in " + filterChannel.Name + " are:\n" + say;
                }
            } else {
                say = "Actually, so far, no reactions have been posted.";
            }
            return say;
        }

    }
}