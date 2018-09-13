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

namespace Nonon
{
    class Program
    {
        private string token = "";
        private const string tokenPath = @"C:\nonon\token.txt";
        private const long targetGuild = 455136595255885844; //deployed on chlo's server
        private const long logChannel = 465508556083429376;
        private const string dataPath = @"C:\nonon\data.json";
        //private const long targetGuild = 313348275455655936; //deployed on my server
        //private const long logChannel = 376933546624811010;
        //private const string dataPath = @"C:\nonon\testData.json";
        DiscordSocketClient client;
        DiscordRestClient rest;
        SocketGuild guild;
        SocketTextChannel log;
        Data data;
        bool scanHistory = false;
        Storer storer = new Storer(dataPath);
        Personality me = new Personality();
        Functions statistics = new Functions();

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            client = new DiscordSocketClient();
            rest = new DiscordRestClient();
            //load bot token.
            if (File.Exists(tokenPath)) {
                token = File.ReadAllText(tokenPath);
            }
            //load or generate data
            if (File.Exists(dataPath)) {
                data = storer.load();
                Console.WriteLine("Loaded data from file.");
            } else {
                data = new Data();
                scanHistory = true;
                Console.WriteLine("Created new data file.");
            }           

            //do connection
            await client.LoginAsync(TokenType.Bot, token);
            await rest.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            //assign events
            client.Log += Log;
            
            client.Ready += async () =>
            {
                //we're connected and ready to join a guild
                guild = client.GetGuild(targetGuild);
                await client.SetStatusAsync(UserStatus.Online);
                await client.SetGameAsync(DateTime.Now.ToString("g"));

                //get log channel
                Console.WriteLine("Connected on " + guild.Name);
                log = guild.GetTextChannel(logChannel);
                Console.WriteLine("Log channel name is " +log.Name);

                if (scanHistory) {
                    try { 
                        await ScanServer(client,rest,guild);
                    } catch (Exception e) {
                        Console.WriteLine(e.Message + "\n\n" + e.StackTrace + "\n\n\n");
                    }
                }
                //assign message events
                client.MessageReceived += MessageReceived;
                client.ReactionAdded += ReactionAdded;

                //return Task.CompletedTask;
            };

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task ScanServer(DiscordSocketClient client, DiscordRestClient rest, SocketGuild guild) {
            bool verbose = false;
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
                var messages = await channel.GetMessagesAsync(1).Flatten();
                var startMessage = messages.FirstOrDefault();
                Console.WriteLine("Start message is " + startMessage.Id);
                if (startMessage != null) {
                    try {
                        messages = await channel.GetMessagesAsync(startMessage.Id, Direction.Before, 100).Flatten();
                        while (messages.Count() != 0) {
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
                }
            }
            //save stats
            storer.save(data);
            //return Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage message)
        {
            //try {
                if (client != null && guild != null) {
                    if (message.Author.Id != client.CurrentUser.Id) {
                        //message came from the outside
                        SocketGuildChannel guildChannel = message.Channel as SocketGuildChannel;
                        SocketTextChannel textChannel = message.Channel as SocketTextChannel;
                        if (guildChannel != null) {
                            if (guildChannel.Guild.Id == guild.Id) {
                                Console.WriteLine(message.Author.Username + " @ " + guildChannel.Guild + " >> " + message.Content);
                                ParseMessage(guildChannel, message);
                                //save stats
                                storer.save(data);
                            //Check for commands ----------------------------------------------------------------
                            foreach (SocketUser mentionedUser in message.MentionedUsers) {
                                    if (mentionedUser.Id == client.CurrentUser.Id) {
                                        //this is a command
                                        Console.WriteLine("$$ " + message.Content);
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
                                            Data.User me = null;
                                            string say = ""; string phrase = "";  int i = 0;
                                            foreach (var user in data.users) {
                                                if (user.id == message.Author.Id) {
                                                    me = user;
                                                    say = "Here are your stats:\n";
                                                    say += "You've sent " + user.messagesSent + " messages during your time here.\n";
                                                    say += "You've also posted " + user.reactions.Count + " reactions.\n";
                                                    //find busiest channel
                                                    Data.Channel bc = null;
                                                    int bcm = 0;
                                                    foreach (var channel in data.channels) {
                                                        foreach (var containedUser in channel.containedUsers) {
                                                            if (containedUser.messagesSent > bcm) {
                                                                bcm = containedUser.messagesSent;
                                                                bc = channel;
                                                            }
                                                        }
                                                    }
                                                    if (bc != null) {
                                                        var channel = guild.GetChannel(bc.id);
                                                        float percent = bc.messageCount / bcm * 100;
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
                                                        say += "\nYour top " + i + " favourite reactions are:\n" + phrase;
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
                                                        i = 0;phrase = "";
                                                        //sort by frequency
                                                        var mentionsList = mentionsDict.ToList();
                                                        mentionsList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
                                                        if (mentionsList.Count != 0) {
                                                            foreach (var mention in mentionsList) {
                                                                i++; if (i > 20) { break; }
                                                                phrase += i + "\t\t" + mention.Key + "\t\tmentioned " + mention.Value + " times\n";
                                                            }
                                                            say += "\nYour " + i + " best friends are:\n" + phrase;
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
                                                Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + statistics.TopChannels(guild, data));
                                                //Console.WriteLine(message.Author.Mention + " " + statistics.TopChannels(guild, data));
                                                break;
                                            case "user":
                                                Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + statistics.TopUsers(guild, data, inChannel));
                                                //Console.WriteLine(message.Author.Mention + " " + statistics.TopUsers(guild, data, inChannel));
                                                break;
                                            case "mention":
                                                Say(message.Channel as SocketTextChannel,message.Author.Mention +" "+ statistics.TopMentions(guild, data, inChannel, byUser));
                                                //Console.WriteLine(message.Author.Mention + " " + statistics.TopMentions(guild, data, inChannel, byUser));
                                                break;
                                            case "reaction":
                                                Say(message.Channel as SocketTextChannel, message.Author.Mention + " " + statistics.TopReactions(guild, data, inChannel, byUser));
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
                            } else {
                                //not targeted guild
                            }
                        } else {
                            //not a guild
                        }
                    } else {
                        //I sent the message
                    }
                } else {
                    Console.WriteLine("client was null");
                }
                //await message.Channel.SendMessageAsync(message.Content);
                return Task.CompletedTask;
            /*} catch (Exception e){
                Console.WriteLine(e.Message);
                //await Say(log,e.Message + "\n```"+e.StackTrace+"```");
            }*/
        }

        private Task ReactionAdded(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel channel, SocketReaction reaction) {
            try {
                if (client != null && guild != null) {
                    SocketGuildChannel guildChannel = channel as SocketGuildChannel;
                    if (guildChannel != null) {
                        if (guildChannel.Guild.Id == guild.Id) {
                            SocketGuildUser user = guild.GetUser(reaction.UserId);
                            if (user != null) {
                                //record stats ------------------------------------------------------------------------------------
                                Console.WriteLine(user.Username + " reacted on " + reaction.Channel.Name + " with " + reaction.Emote.Name);
                                //update stats on the server
                                data.AddReaction(reaction);
                                //update stats on the user
                                Data.User dataUser = data.FindUser(user as SocketUser);
                                dataUser.AddReaction(reaction);
                                //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.reactions.Count + " reactions.");
                                //update stats on the channel
                                Data.Channel dataChannel = data.FindChannel(reaction.Channel);
                                dataChannel.AddReaction(reaction);
                                Data.User dataChannelUser = dataChannel.FindUser(user as SocketUser);
                                dataChannelUser.AddReaction(reaction);
                                //save stats
                                storer.save(data);
                            }
                        }
                    }
                }
            } catch (Exception e){
                Console.WriteLine(e.Message);
            }
            return Task.CompletedTask;
        }

        private async Task Say(SocketTextChannel channel, string message) {
            Console.WriteLine(channel.Name + " << " + message);
            await channel.SendMessageAsync(message);
            //return Task.CompletedTask;
        }
        private async Task DirectSay(SocketUser user, string message) {
            Console.WriteLine(user.Username + " << " + message);
            await user.SendMessageAsync(message);
            //return Task.CompletedTask;
        }

        static string bestName(SocketGuildUser user) {
            return user.Nickname == null ? user.Username : user.Nickname;
        }
        static string bestName(SocketUser user) {
            return (user as SocketGuildUser).Nickname == null ? user.Username : (user as SocketGuildUser).Nickname;
        }

        private void ParsePastMessage(SocketGuildChannel guildChannel, IMessage message) {
            string[] messageWords = message.Content.Split(null);
            //record stats ------------------------------------------------------------------------------------
            //update stats on the user
            Data.User dataUser = data.FindUser(message.Author);
            dataUser.messagesSent += 1;
            //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.messagesSent + " messages.");
            //update stats on the channel
            Data.Channel dataChannel = data.FindChannel(message.Channel);
            dataChannel.messageCount += 1;
            Data.User dataChannelUser = dataChannel.FindUser(message.Author);
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
                foreach (Data.User user in data.users) {
                    suser = client.GetUser(user.id);
                    guser = guild.GetUser(user.id);
                    mentioned = false;
                    if (suser != null && suser.Username.ToLower() == word.ToLower()) { mentioned = true; }
                    if (guser != null && bestName(guser).ToLower() == word.ToLower()) { mentioned = true; }
                    if (mentioned) {
                        data.AddMention(suser);
                        dataUser.AddMention(suser);
                        dataChannel.AddMention(suser);
                        dataChannelUser.AddMention(suser);
                    }
                }
            }

            //check for reactions
        }

        private void ParseMessage(SocketGuildChannel guildChannel, SocketMessage message) {
            string[] messageWords = message.Content.Split(null);
            //record stats ------------------------------------------------------------------------------------
            //update stats on the user
            Data.User dataUser = data.FindUser(message.Author);
            dataUser.messagesSent += 1;
            //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.messagesSent + " messages.");
            //update stats on the channel
            Data.Channel dataChannel = data.FindChannel(message.Channel);
            dataChannel.messageCount += 1;
            Data.User dataChannelUser = dataChannel.FindUser(message.Author);
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
                foreach (Data.User user in data.users) {
                    suser = client.GetUser(user.id);
                    guser = guild.GetUser(user.id);
                    mentioned = false;
                    if (suser != null && suser.Username.ToLower() == word.ToLower()) { mentioned = true; }
                    if (guser != null && bestName(guser).ToLower() == word.ToLower()) { mentioned = true; }
                    if (mentioned) {
                        data.AddMention(suser);
                        dataUser.AddMention(suser);
                        dataChannel.AddMention(suser);
                        dataChannelUser.AddMention(suser);
                    }
                }
            }
        }

        public class Functions
        {
            public Functions() {
            }

            public string TopChannels(SocketGuild guild, Data data) {
                Dictionary <ulong, int> channels = new Dictionary<ulong, int>();
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
                say = "The top " + i + " most used channels are:\n" + say;
                return say;
            }

            public string TopUsers(SocketGuild guild, Data data, SocketTextChannel filterChannel) {
                //describe stats on users
                //return dictionary of matching users
                Dictionary<ulong, int> users = new Dictionary<ulong, int>(); //dictionary is users and message count
                //get data objects
                Data.Channel filterDataChannel = null;
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
                                say += i + "\t\t" + bestName(guser) + "\t\thas sent " + user.Value + " messages\n";
                            }
                        }
                        say = "The top " + i + " most verbose users on " + guild.Name + " are:\n" + say;
                    } else {
                        foreach (var user in usersList) {
                            SocketGuildUser guser = guild.GetUser(user.Key);
                            if (guser != null) {
                                i++; if (i > 10) { break; }
                                say += i + "\t\t" + bestName(guild.GetUser(user.Key)) + "\t\thas sent " + user.Value + " messages\n";
                            }
                        }
                        say = "The top " + i + " most active denizens of " + filterChannel.Name + " are:\n" + say;
                    }
                } else {
                    say = "sorry, it Looks like no users have spoken there yet!";
                }
                return say;
            }
            
            public string TopMentions(SocketGuild guild, Data data,SocketTextChannel filterChannel, SocketUser filterUser) {
                //describe stats on mentions
                //return dictionary of matching mentions
                Console.WriteLine("Getting mentions.");
                Console.WriteLine("Filter channel is " + (filterChannel == null ? "null" : filterChannel.Id.ToString()));
                Console.WriteLine("Filter user is " + (filterUser == null ? "null" : filterUser.Id.ToString()));
                
                Dictionary<ulong, int> mentions = new Dictionary<ulong, int>(); //dictionary is users mentioned by frequency
                //get data objects
                Data.User filterDataUser = null;
                Data.Channel filterDataChannel = null;
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
                            if (i > 10) { break; } i++;
                            say += i + "\t\t" + bestName(guild.GetUser(mention.Key)) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                        say = "The top " + i + " most popular users on "+guild.Name+" are:\n" + say;
                    } else if (filterChannel == null) {
                        foreach (var mention in mentionsList) {
                            if (i > 10) { break; } i++;
                            say += i + "\t\t" + bestName(guild.GetUser(mention.Key)) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                        say = bestName(filterUser) + "'s " + i + " best friends are:\n" + say;
                    } else if (filterUser == null) {
                        foreach (var mention in mentionsList) {
                            if (i > 10) { break; } i++;
                            say += i + "\t\t" + bestName(guild.GetUser(mention.Key)) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                        say = "The top "+ i +" most popular users in " + filterChannel.Name +" are:\n" + say;
                    } else {
                        foreach (var mention in mentionsList) {
                            if (i > 10) { break; } i++;
                            say += i + "\t\t" + bestName(guild.GetUser(mention.Key)) + "\t\tmentioned " + mention.Value + " times\n";
                        }
                        say = bestName(filterUser) + "'s " + i + " favourite people when chatting in " + filterChannel.Name + " are:\n" + say;
                    }
                } else {
                    say = "sorry, it Looks like no mentions have occurred yet!";
                }
                return say;
            }

            public string TopReactions(SocketGuild guild, Data data, SocketTextChannel filterChannel, SocketUser filterUser) {
                //describe stats on reactions
                Console.WriteLine("Getting mentions.");
                Console.WriteLine("Filter channel is " + (filterChannel == null ? "null" : filterChannel.Id.ToString()));
                Console.WriteLine("Filter user is " + (filterUser == null ? "null" : filterUser.Id.ToString()));

                Dictionary<string, int> reactions = new Dictionary<string, int>(); //dictionary is reactions by frequency
                Dictionary<ulong, int> users = new Dictionary<ulong, int>(); //dictionary is users by reaction count
                //get data objects
                Data.User filterDataUser = null;
                Data.Channel filterDataChannel = null;
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
                        say = "The top " + i + " most popular reactions on " + guild.Name + " are:\n" + say;
                        i = 0;
                        if (usersList.Count != 0) {
                            foreach (var user in usersList) {
                                i++; if (i > 10) { break; }
                                phrase += i + "\t\t" + guild.GetUser(user.Key) + "\t\thas posted " + user.Value + " reactions\n";
                            }
                            say += "\n"+ guild.Name +"'s "+i+" biggest reactors are:\n" + phrase;
                        }
                    } else if (filterChannel == null) {
                        say = bestName(filterUser) + "'s " + i + " favourite reactions are:\n" + say;
                    } else if (filterUser == null) {
                        say = "The top " + i + " most popular reactions used in " + filterChannel.Name + " are:\n" + say;
                    } else {
                        say = bestName(filterUser) + "'s " + i + " favourite reactions when chatting in " + filterChannel.Name + " are:\n" + say;
                    }
                } else {
                    say = "Actually, so far, no reactions have been posted.";
                }
                return say;
            }

            
        }
    }
    class StringEater
    {
        //used to consume a command string word by word.
        string message;
        Queue<string> queue;
        public StringEater(string message) {
            this.message = message;
            string[] words = message.Split(null);
            queue = new Queue<string>();
            foreach (string word in words) {
                if (word != null) { queue.Enqueue(word); }
            }
        }
        public bool Find(string search) {
            //consumes the next word if it matches
            if (queue.Count == 0) {
                return false;
            } else {
                if (queue.Peek() == search) {
                    queue.Dequeue();
                    return true;
                } else {
                    return false;
                }
            }
        }
        public string Get() {
            return queue.Dequeue();
        }
    }
    class Data 
    {
        /* This class stores all recorded statistics throughout the server. Data is organised into objects.
         * The classes represent information stored about the discord items they represent.
         * Constructing a new instance of these classes is analogous to adding a new record of information about an object.
         */
        public List<User> users;
        public List<Channel> channels;
        public List<Mention> mentions;
        public List<Reaction> reactions;
        public bool populated = false;

        public Data() {
            users = new List<User>();
            channels = new List<Channel>();
            mentions = new List<Mention>();
            reactions = new List<Reaction>();
        }

        public User FindUser(IUser source) {
            var user = users.Where(item => item.id == source.Id).FirstOrDefault();
            if (user == null) {
                //we didnt find one. so let's make and add one.
                users.Add(new User(source));
                user = users.Where(item => item.id == source.Id).FirstOrDefault();
            }
            return user;
        }
        public Channel FindChannel (IMessageChannel source) {
            var channel = channels.Where(item => item.id == source.Id).FirstOrDefault();
            if (channel == null) {
                //we didnt find one. so let's make and add one.
                channels.Add(new Channel(source));
                channel = channels.Where(item => item.id == source.Id).FirstOrDefault();
            }
            return channel;
        }
        public void AddMention (SocketUser mentionedUser) {
            mentions.Add(new Mention(mentionedUser));
        }
        public void AddReaction(SocketReaction reaction) {
            reactions.Add(new Reaction(reaction));
        }
        public class User : DataObject
        {
            public ulong id;
            public List<Mention> mentions;
            public List<Reaction> reactions;
            public int messagesSent;

            public User() {}
            public User(IUser user) {
                id = user.Id;
                messagesSent = 0;
                mentions = new List<Mention>();
                reactions = new List<Reaction>();
            }
            public void AddMention(SocketUser mentionedUser) {
                mentions.Add(new Mention(mentionedUser));
            }
            public void AddReaction(SocketReaction reaction) {
                reactions.Add(new Reaction(reaction));
            }
        }
        public class Channel : DataObject
        {
            public ulong id;
            public List<User> containedUsers;
            public List<Mention> containedMentions;
            public List<Reaction> containedReactions;
            public int messageCount;
            public Channel() {}
            public Channel(IMessageChannel channel) {
                id = channel.Id;
                containedUsers = new List<User>();
                containedMentions = new List<Mention>();
                containedReactions = new List<Reaction>();
                messageCount = 0;
            }
            public User FindUser(IUser source) {
                //try to find a contained user or add one if we didn't find one
                var user = containedUsers.Where(item => item.id == source.Id).FirstOrDefault();
                if (user == null) {
                    //we didnt find one. so let's make and add one.
                    user = new User(source);
                    containedUsers.Add(user);
                }
                return user;
            }
            public void AddMention(IUser mentionedUser) {
                containedMentions.Add(new Mention(mentionedUser));
            }
            public void AddReaction(SocketReaction reaction) {
                containedReactions.Add(new Reaction(reaction));
            }
        }
        public class Mention : DataObject
        {
            public ulong mentionedId;
            public Mention(){}
            public Mention(IUser mentionedUser) {
                if (mentionedUser != null) {
                    mentionedId = mentionedUser.Id;
                }
            }
        }
        public class Reaction : DataObject
        {
            public ulong messageId;
            public ulong userId;
            public string emotename;
            public Reaction() { }
            public Reaction(SocketReaction reaction) {
                messageId = reaction.MessageId;
                userId = reaction.UserId;
                emotename = reaction.Emote.Name;
            }
        }
        public class DataObject
        {
            public ulong id;
        }
    }
    class Storer
    {
        string dataPath;
        public Storer(string dataPath) {
            this.dataPath = dataPath;
        }
        public Data load() {
            string text = File.ReadAllText(dataPath);
            Data data = JsonConvert.DeserializeObject<Data>(text);
            return data;
        }
        public void save(Data data) {
            //open file stream
            using (StreamWriter file = File.CreateText(dataPath)) {
                JsonSerializer serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, data);
            }
        }
    }
    class Personality
    {
        string[] greetings;

        public Personality() {
            greetings = new string[] {"Hallo friends","Good morning all","it me again","hello beautiful people","hi im back again","its me again sorry lol","hi hello hey hi"};
        }

        public string getGreeting() {
            Random rnd = new Random();
            int r = rnd.Next(greetings.Length);
            return greetings[r];
        }
    }


    
}
