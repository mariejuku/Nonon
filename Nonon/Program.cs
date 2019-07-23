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
        //private const long targetGuild = 455136595255885844; //deployed on chlo's server
        //private const long logChannel = 465508556083429376;
        private const string statisticsPath = @"C:\nonon\stats.json";
        private const string settingsPath = @"C:\nonon\settings.json";
        private const string memoryPath = @"C:\nonon\memory.json";
        private const long targetGuild = 313348275455655936; //deployed on my server
        private const long logChannel = 603035065190187009;
        //private const string dataPath = @"C:\nonon\testData.json";
        public DiscordSocketClient client;
        DiscordRestClient rest;
        public SocketGuild guild;
        public SocketTextChannel log;
        StatsDriver statsDriver;
        Statistics statistics;
        MemoryDriver memoryDriver;
        bool scanHistory = false;
        Storer statsStorer = new Storer(statisticsPath);
        Storer memoryStorer = new Storer(memoryPath);
        Personality me = new Personality();
        CommandsDriver commands;
        

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
            //load or generate data file: statistics
            if (File.Exists(statisticsPath)) {
                statistics = statsStorer.loadStatistics();
                Console.WriteLine("Loaded data from file.");
            } else {
                statistics = new Statistics();
                scanHistory = false;
                Console.WriteLine("Created new data file.");
            }

            //do connection
            await client.LoginAsync(TokenType.Bot, token);
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

                //load up the stats driver
                statsDriver = new StatsDriver(this, statsStorer, log, statistics, guild, client);

                //load bot command parser
                commands = new CommandsDriver(this, statsStorer, log, statistics, guild, client);
                commands.stats = statsDriver;

                //load bot memory
                memoryDriver = new MemoryDriver(this, memoryPath);

                if (scanHistory) {
                    try { 
                        await statsDriver.ScanServer(client,guild);
                    } catch (Exception e) {
                        Console.WriteLine(e.Message + "\n\n" + e.StackTrace + "\n\n\n");
                    }
                }
                //assign message events
                client.MessageReceived += MessageReceived;
                client.ReactionAdded += ReactionAdded;

                //return Task.CompletedTask;
                Say(log, me.getGreeting());
            };

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
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
                            statsDriver.ParseMessage(guildChannel, message);
                            //save stats
                            statsStorer.save(statistics);
                            //Check for commands ----------------------------------------------------------------
                            //if the incoming message is a command, send it to the command parser and let it be handled there.
                            //if the incoming message is just chat, send it to the learning stuff.
                            bool weAreMentioned = false;
                            foreach (SocketUser mentionedUser in message.MentionedUsers) {
                                if (mentionedUser.Id == client.CurrentUser.Id) {
                                    weAreMentioned = true;
                                }
                            }
                            if (weAreMentioned) {
                                Console.WriteLine("<" + guildChannel.Guild + "> " + message.Author.Username + " @ " + guildChannel.Name + " [mentioned me] >> " + message.Content);
                                //this is a command
                                Console.WriteLine("$$ " + message.Content);
                                commands.Parse(message);
                            } else {
                                Console.WriteLine("<" + guildChannel.Guild + "> " + message.Author.Username + " @ " + guildChannel.Name + " >> " + message.Content);
                                //this is just chat.
                                memoryDriver.Parse(message);
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
                                statistics.AddReaction(reaction);
                                //update stats on the user
                                Statistics.User dataUser = statistics.FindUser(user as SocketUser);
                                dataUser.AddReaction(reaction);
                                //Console.WriteLine("User " + dataUser.id + " has sent " + dataUser.reactions.Count + " reactions.");
                                //update stats on the channel
                                Statistics.Channel dataChannel = statistics.FindChannel(reaction.Channel);
                                dataChannel.AddReaction(reaction);
                                Statistics.User dataChannelUser = dataChannel.FindUser(user as SocketUser);
                                dataChannelUser.AddReaction(reaction);
                                //save stats
                                statsStorer.save(statistics);
                            }
                        }
                    }
                }
            } catch (Exception e){
                Console.WriteLine(e.Message);
            }
            return Task.CompletedTask;
        }

        public async Task Say(SocketTextChannel channel, string message) {
            Console.WriteLine("<" + channel.Guild.Name +"> Nonon @ " + channel.Name + " << " + message);
            await channel.SendMessageAsync(message);
            //return Task.CompletedTask;
        }
        public async Task DirectSay(SocketUser user, string message) {
            Console.WriteLine("<Direct Message> Nonon @ " + bestName(user) + "<< " + message);
            await user.SendMessageAsync(message);
            //return Task.CompletedTask;
        }

        static string bestName(SocketGuildUser user) {
            return user.Nickname == null ? user.Username : user.Nickname;
        }
        static string bestName(SocketUser user) {
            return (user as SocketGuildUser).Nickname == null ? user.Username : (user as SocketGuildUser).Nickname;
        }
        public string getBestName(SocketGuildUser user) {
            return bestName(user);
        }
        public string getBestName(SocketUser user) {
            return bestName(user);
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
        public int Count() {
            return queue.Count;
        }
    }
    class Statistics 
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

        public Statistics() {
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
        public Statistics loadStatistics() {
            string text = File.ReadAllText(dataPath);
            Statistics data = JsonConvert.DeserializeObject<Statistics>(text);
            return data;
        }
        public Memory loadMemory() {
            string text = File.ReadAllText(dataPath);
            Memory data = JsonConvert.DeserializeObject<Memory>(text);
            return data;
        }
        public void save(Object data) {
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