using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Nonon
{
    class Program
    {
        private string token = "";
        private const long targetGuild = 455136595255885844;
        private const long logChannel = 465508556083429376;
        DiscordSocketClient client;
        SocketGuild guild;
        SocketTextChannel log;
        Data data;
        Storer storer = new Storer();

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            client = new DiscordSocketClient();

            //load bot token.
            if (File.Exists(@"c:\nonon\token.txt")) {
                token = File.ReadAllText(@"C:\nonon\token.txt");
            }
                //load or generate data
                if (File.Exists(@"c:\nonon\data.json")) {
                data = storer.load();
                Console.WriteLine("Loaded data from file.");
            } else {
                data = new Data();
                Console.WriteLine("Created new data file.");
            }           

            //do connection
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            //assign events
            client.Log += Log;
            client.MessageReceived += MessageReceived;
            client.ReactionAdded += ReactionAdded;
            client.Ready += async () =>
            {
                //we're connected and ready to join a guild
                guild = client.GetGuild(targetGuild);
                await client.SetStatusAsync(UserStatus.Online);
                await client.SetGameAsync("Heckin wowee my buds");

                //get log channel
                Console.WriteLine("Connected on " + guild.Name);
                log = guild.GetTextChannel(logChannel);
                Console.WriteLine("Log channel name is " +log.Name);
                await Say(log as SocketTextChannel, "hallo friends");
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

        private async Task MessageReceived(SocketMessage message)
        {
            try {
                if (client != null && guild != null) {
                    if (message.Author.Id != client.CurrentUser.Id) {
                        //message came from the outside
                        SocketGuildChannel guildChannel = message.Channel as SocketGuildChannel;
                        SocketTextChannel textChannel = message.Channel as SocketTextChannel;
                        if (guildChannel != null) {
                            if (guildChannel.Guild.Id == guild.Id) {
                                string[] messageWords = message.Content.Split(null);
                                //record stats ------------------------------------------------------------------------------------
                                Console.WriteLine(message.Author.Username + " @ " + guildChannel.Guild + " >> " + message.Content);
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
                                        if (guser != null && guser.Nickname.ToLower() == word.ToLower()) { mentioned = true; }
                                        if (mentioned) {
                                            data.AddMention(suser);
                                            dataUser.AddMention(suser);
                                            dataChannel.AddMention(suser);
                                            dataChannelUser.AddMention(suser);
                                        }
                                    }
                                }
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
                                            await Say(message.Channel as SocketTextChannel, message.Author.Mention + ", I sent you the help list.");
                                            await Say(message.Channel as SocketTextChannel, message.Author.Mention + "Keep in mind I'm still kinda WIP and things may break lol.");
                                            string s = "Here are my commands.\n" +
                                                "They work in a logical way. Ask for an item to see the counts of that item on the server.\n" +
                                                "Item can be users, channels, reacts, or mentions.\n" +
                                                "add 'in <channel name>' to an item command to see just the items occurring in that channel.\n" +
                                                "add 'by <user name>' to an item command to see just the items produced by that user.\n\n" +
                                                "help\t\tView this list.\n" +
                                                "me\t\tShows your stats.\n" +
                                                "<item>\t\tShows stats for this kind of item.\n" +
                                                "top <item>\t\tShows the top 10 of the item.\n" +
                                                "\t\t\tYou may also use 'in' and 'by' keywords for this command.\n" +
                                                "user <username>\t\tShows the stats for this user.\n" +
                                                "channel <channelname>\t\tShows the stats for this channel.\n" +
                                                "server\t\tShows the stats for the server.\n" +
                                                "friendships\t\tShows the biggest friendships on the server.\n";
                                            await DirectSay(message.Author, s);
                                        } else if (command.Find("me")) {
                                            Data.User me = null;
                                            string say = "";
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
                                                            "That's " + percent + "% of that channel's discussion.";
                                                    }
                                                    //todo: find favourite reaction
                                                }
                                            }
                                            if (me == null) {
                                                say = "Sorry, I don't have information about you.";
                                            }
                                            await Say(message.Channel as SocketTextChannel, message.Author.Mention + say);
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
                                                        byUser = user;
                                                    }
                                                }
                                            }
                                            if (command.Find("in")) {
                                                query = command.Get();
                                                //they specified a channel so let's go find it
                                                foreach (var channel in guild.TextChannels) {
                                                    if (channel.Mention == query || channel.Name == query) {
                                                        inChannel = channel;
                                                    }
                                                }
                                            }
                                            Dictionary<ulong, int> ids;
                                            List<KeyValuePair<ulong, int>> idlist;
                                            switch (selectedItem) {
                                                case "channel":
                                                    //TOP CHANNELS
                                                    //construct channel dictionary
                                                    ids = new Dictionary<ulong, int>();
                                                    foreach (var channel in data.channels) {
                                                        ids.Add(channel.id, channel.messageCount);
                                                    }
                                                    //sort by frequency
                                                    idlist = ids.ToList();
                                                    idlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                    //print
                                                    i = 0; say = "";
                                                    foreach (var item in idlist) {
                                                        i++; if (i > 10) { break; }
                                                        say += i + "\t\t" + guild.GetChannel(item.Key).Name + "\t\twith" + item.Value + " messages\n";
                                                    }
                                                    say = "The top " + i + " most used channels are:\n" + say;
                                                    break;
                                                case "user":
                                                    //construct user dictionary
                                                    ids = null;
                                                    if (inChannel != null) {
                                                        //TOP USERS
                                                        ids = new Dictionary<ulong, int>();
                                                        foreach (var user in data.users) {
                                                            ids.Add(user.id, user.messagesSent);
                                                        }
                                                    } else {
                                                        //TOP USERS IN CHANNEL
                                                        foreach (var channel in data.channels) {
                                                            if (channel.id == inChannel.Id) {
                                                                ids = new Dictionary<ulong, int>();
                                                                foreach (var user in channel.containedUsers) {
                                                                    ids.Add(user.id, user.messagesSent);
                                                                }
                                                            }
                                                        }
                                                    }
                                                    if (ids != null) {
                                                        //sort by frequency
                                                        idlist = ids.ToList();
                                                        idlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                        //print
                                                        i = 0; say = "";
                                                        foreach (var item in idlist) {
                                                            i++; if (i > 10) { break; }
                                                            say += i + "\t\t" + guild.GetChannel(item.Key).Name + "\t\twith" + item.Value + " messages\n";
                                                        }
                                                        say = "The top " + i + " most active users" + (inChannel == null ? " " : " in " + inChannel.Name + " ") + "are:\n" + say;
                                                    }
                                                    break;
                                                case "mention":
                                                    //construct mention dictionary
                                                    ids = null; //dictionary is users mentioned by frequency
                                                    if (byUser == null) {
                                                        if (inChannel == null) {
                                                            //TOP MENTIONS 
                                                            ids = new Dictionary<ulong, int>();
                                                            foreach (var mention in data.mentions) {
                                                                if (!ids.ContainsKey(mention.mentionedId)) {
                                                                    ids.Add(mention.mentionedId, 0);
                                                                }
                                                                ids[mention.mentionedId]++;
                                                            }
                                                        } else {
                                                            //TOP MENTIONS IN CHANNEL
                                                            foreach (var channel in data.channels) {
                                                                if (channel.id == inChannel.Id) {
                                                                    ids = new Dictionary<ulong, int>();
                                                                    foreach (var mention in channel.containedMentions) {
                                                                        if (!ids.ContainsKey(mention.mentionedId)) {
                                                                            ids.Add(mention.mentionedId, 0);
                                                                        }
                                                                        ids[mention.mentionedId]++;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    } else {
                                                        if (inChannel == null) {
                                                            //TOP MENTIONS BY USER
                                                            foreach (var user in data.users) {
                                                                if (user.id == byUser.Id) {
                                                                    ids = new Dictionary<ulong, int>();
                                                                    foreach (var mention in user.mentions) {
                                                                        if (!ids.ContainsKey(mention.mentionedId)) {
                                                                            ids.Add(mention.mentionedId, 0);
                                                                        }
                                                                        ids[mention.mentionedId]++;
                                                                    }
                                                                }
                                                            }
                                                        } else {
                                                            //TOP MENTIONS BY USER IN CHANNEL
                                                            foreach (var channel in data.channels) {
                                                                if (channel.id == inChannel.Id) {
                                                                    foreach (var user in channel.containedUsers) {
                                                                        if (user.id == byUser.Id) {
                                                                            ids = new Dictionary<ulong, int>();
                                                                            foreach (var mention in user.mentions) {
                                                                                if (!ids.ContainsKey(mention.mentionedId)) {
                                                                                    ids.Add(mention.mentionedId, 0);
                                                                                }
                                                                                ids[mention.mentionedId]++;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    if (ids != null) {
                                                        //sort by frequency
                                                        idlist = ids.ToList();
                                                        idlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                        //print
                                                        i = 0; say = "";
                                                        foreach (var item in idlist) {
                                                            i++; if (i > 10) { break; }
                                                            say += i + "\t\t" + guild.GetChannel(item.Key).Name + "\t\twith" + item.Value + " messages\n";
                                                        }
                                                        say = "The top " + i + " most common mentions" + (byUser == null ? "" : " by " + byUser.Username + " ") +
                                                             (inChannel == null ? " " : " in " + inChannel.Name + " ") + "are:\n" + say;
                                                    }
                                                    break;
                                                case "reaction":
                                                    //construct reaction dictionary
                                                    Dictionary<string, int> strings = null;
                                                    List<KeyValuePair<string, int>> stringlist;
                                                    if (byUser == null) {
                                                        if (inChannel == null) {
                                                            //TOP REACTIONS 
                                                            say = "Here are the top reactions stats.\n";
                                                            string t = "";
                                                            //TOP REACTIONS > MOST USED REACTS
                                                            strings = new Dictionary<string, int>(); //emote name by frequency
                                                            foreach (var reaction in data.reactions) {
                                                                if (!strings.ContainsKey(reaction.emotename)) {
                                                                    strings.Add(reaction.emotename, 0);
                                                                }
                                                                strings[reaction.emotename]++;
                                                            }
                                                            if (strings != null) {
                                                                //sort by frequency
                                                                stringlist = strings.ToList();
                                                                stringlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                                //print
                                                                i = 0;
                                                                foreach (var item in stringlist) {
                                                                    i++; if (i > 10) { break; }
                                                                    t += i + "\t\t" + item.Key + " has been used " + item.Value + " times\n";
                                                                }
                                                                say += "The top " + i + " most used reacts are:\n" + t;
                                                            }
                                                            //TOP REACTIONS > MOST REACTING USERS
                                                            t = "";
                                                            ids = new Dictionary<ulong, int>(); //reaction author by frequency
                                                            foreach (var reaction in data.reactions) {
                                                                if (!ids.ContainsKey(reaction.userId)) {
                                                                    ids.Add(reaction.userId, 0);
                                                                }
                                                                ids[reaction.userId]++;
                                                            }
                                                            if (ids != null) {
                                                                //sort by frequency
                                                                idlist = ids.ToList();
                                                                idlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                                //print
                                                                i = 0;
                                                                foreach (var item in idlist) {
                                                                    i++; if (i > 10) { break; }
                                                                    t += i + "\t\t" + guild.GetUser(item.Key).Username + " has posted " + item.Value + " reactions\n";
                                                                }
                                                                say += "\nThe top " + i + " reacting users are:\n" + t;
                                                            }
                                                        } else {
                                                            //TOP REACTIONS IN CHANNEL 
                                                            foreach (var channel in data.channels) {
                                                                if (channel.id == inChannel.Id) {
                                                                    say = "Here are the reactions stats for " + inChannel.Name + ". \n";
                                                                    string t = "";
                                                                    //TOP REACTIONS IN CHANNEL > MOST USED REACTS
                                                                    strings = new Dictionary<string, int>(); //emote name by frequency
                                                                    foreach (var reaction in channel.containedReactions) {
                                                                        if (!strings.ContainsKey(reaction.emotename)) {
                                                                            strings.Add(reaction.emotename, 0);
                                                                        }
                                                                        strings[reaction.emotename]++;
                                                                    }
                                                                    if (strings != null) {
                                                                        //sort by frequency
                                                                        stringlist = strings.ToList();
                                                                        stringlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                                        //print
                                                                        i = 0;
                                                                        foreach (var item in stringlist) {
                                                                            i++; if (i > 10) { break; }
                                                                            t += i + "\t\t" + item.Key + " has been used " + item.Value + " times\n";
                                                                        }
                                                                        say += "The top " + i + " most used reacts are:\n" + t;
                                                                    }
                                                                    //TOP REACTIONS IN CHANNEL > MOST REACTING USERS
                                                                    t = "";
                                                                    ids = new Dictionary<ulong, int>(); //reaction author by frequency
                                                                    foreach (var reaction in channel.containedReactions) {
                                                                        if (!ids.ContainsKey(reaction.userId)) {
                                                                            ids.Add(reaction.userId, 0);
                                                                        }
                                                                        ids[reaction.userId]++;
                                                                    }
                                                                    if (ids != null) {
                                                                        //sort by frequency
                                                                        idlist = ids.ToList();
                                                                        idlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                                        //print
                                                                        i = 0;
                                                                        foreach (var item in idlist) {
                                                                            i++; if (i > 10) { break; }
                                                                            t += i + "\t\t" + guild.GetUser(item.Key).Username + " has posted " + item.Value + " reactions\n";
                                                                        }
                                                                        say += "\nThe top " + i + " reacting users are:\n" + t;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    } else {
                                                        if (inChannel == null) {
                                                            //TOP REACTIONS BY USER
                                                            foreach (var user in data.users) {
                                                                if (user.id == byUser.Id) {
                                                                    sayChunk = "";
                                                                    strings = new Dictionary<string, int>(); //emote name by frequency
                                                                    foreach (var reaction in user.reactions) {
                                                                        if (!strings.ContainsKey(reaction.emotename)) {
                                                                            strings.Add(reaction.emotename, 0);
                                                                        }
                                                                        strings[reaction.emotename]++;
                                                                    }
                                                                    if (strings != null) {
                                                                        //sort by frequency
                                                                        stringlist = strings.ToList();
                                                                        stringlist.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                                                                        //print
                                                                        i = 0;
                                                                        foreach (var item in stringlist) {
                                                                            i++; if (i > 10) { break; }
                                                                            sayChunk += i + "\t\t" + item.Key + " has been used " + item.Value + " times\n";
                                                                        }
                                                                        say += "Here are " + byUser.Username + "'s " + i + " favourite reactions:\n" + sayChunk;
                                                                    }
                                                                }
                                                            }
                                                        } else {
                                                            //TOP REACTIONS BY USER IN CHANNEL
                                                            foreach (var channel in data.channels) {
                                                                if (channel.id == inChannel.Id) {
                                                                    foreach (var user in channel.containedUsers) {
                                                                        if (user.id == byUser.Id) {
                                                                            strings = new Dictionary<string, int>();
                                                                            foreach (var reaction in user.reactions) {
                                                                                if (!strings.ContainsKey(reaction.emotename)) {
                                                                                    strings.Add(reaction.emotename, 0);
                                                                                }
                                                                                strings[reaction.emotename]++;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    break;
                                                default:
                                                    say = "Sorry friend, but I don't understand that sequence.";
                                                    break;
                                            }
                                            await Say(message.Channel as SocketTextChannel, say);
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
                //return Task.CompletedTask;
            } catch (Exception e){
                Console.WriteLine(e.Message);
            }
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

        public Data() {
            users = new List<User>();
            channels = new List<Channel>();
            mentions = new List<Mention>();
            reactions = new List<Reaction>();
        }
        public User FindUser(SocketUser source) {
            var user = users.Where(item => item.id == source.Id).FirstOrDefault();
            if (user == null) {
                //we didnt find one. so let's make and add one.
                users.Add(new User(source));
                user = users.Where(item => item.id == source.Id).FirstOrDefault();
            }
            return user;
        }
        public Channel FindChannel (ISocketMessageChannel source) {
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
            public User(SocketUser user) {
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
            public Channel(ISocketMessageChannel channel) {
                id = channel.Id;
                containedUsers = new List<User>();
                containedMentions = new List<Mention>();
                containedReactions = new List<Reaction>();
                messageCount = 0;
            }
            public User FindUser(SocketUser source) {
                //try to find a contained user or add one if we didn't find one
                var user = containedUsers.Where(item => item.id == source.Id).FirstOrDefault();
                if (user == null) {
                    //we didnt find one. so let's make and add one.
                    user = new User(source);
                    containedUsers.Add(user);
                }
                return user;
            }
            public void AddMention(SocketUser mentionedUser) {
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
            public Mention(SocketUser mentionedUser) {
                //Console.WriteLine("Recorded new mention of " + mentionedUser.Username);
                mentionedId = mentionedUser.Id;
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
        public Data load() {
            string text = File.ReadAllText(@"C:\nonon\data.json");
            Data data = JsonConvert.DeserializeObject<Data>(text);
            return data;
        }
        public void save(Data data) {
            //open file stream
            using (StreamWriter file = File.CreateText(@"C:\nonon\data.json")) {
                JsonSerializer serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, data);
            }
        }
    }
}
