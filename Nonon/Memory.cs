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
using Syn.WordNet;

namespace Nonon {
    class MemoryDriver {
        Program program; //reference to the bot
        Memory memory; //reference to bot memory
        WordNetEngine wordNet;
        string dataPath; //save and load location
        SocketTextChannel log;
        List<SocketMessage> pastMessages = new List<SocketMessage>(); //used for keeping track of conversation direction

        public MemoryDriver(Program p, string d) {
            program = p; dataPath = d;
            log = program.log;
            //load or generate file.
            if (File.Exists(dataPath)) {
                memory = load(dataPath);
                Console.WriteLine("!! Startup: Loaded memory from file.");
            } else {
                memory = new Memory();
                Console.WriteLine("!! Startup: Created new memory file.");
            }

            //scan through the users list for this server and memorize any new users.
            foreach (SocketGuildUser user in program.guild.Users) {
                if (!memory.users.ContainsKey(user.Username)) {
                    memory.AddUser(user);
                    program.Say(log, "Spotted "+user.Username);
                }
            }
        }

        private async Task Say(SocketTextChannel log, string message) { program.Say(log, message); }
        private async Task DirectSay(SocketUser user, string message) { program.DirectSay(user, message); }

        private Memory load(string dataPath) {
            string text = File.ReadAllText(dataPath);
            Memory data = JsonConvert.DeserializeObject<Memory>(text);
            return data;
        }
        public void save(Memory data) {
            //open file stream
            using (StreamWriter file = File.CreateText(dataPath)) {
                JsonSerializer serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, data);
            }
        }

        public void Parse (SocketMessage socketMessage) {
            string message = socketMessage.Content.ToLower();
            string output = "";
            
            //If there is no discord user associated with this message yet, add one.
            if (!memory.users.ContainsKey(socketMessage.Author.Username)) {
                memory.AddUser((SocketGuildUser)socketMessage.Author);
            }
            //locate the user who spoke.
            Memory.User user = memory.users[socketMessage.Author.Username];
            user.speakingTo = FindMessageTarget(socketMessage);
            message = Unpronounify(message, user);
            
            program.Say(log, output);
        }

        public string FindMessageTarget(SocketMessage socketMessage) {
            string messageTarget = null;
            string authorName = socketMessage.Author.Username;
            //try to parse who they are speaking to.
            if (socketMessage.MentionedUsers.Count > 0) {
                //if they mentioned someone then that's pretty easy.
                foreach (SocketUser mentionedUser in socketMessage.MentionedUsers) {
                    if (mentionedUser.Username != authorName) { //but obviously they arent talking to themselves probably.
                        messageTarget = mentionedUser.Username;
                    }
                }
            } else {
                //if they didnt directly mention somebody, maybe if they still said someone's name or nickname. use a stringEater to check. 
                StringEater nameChecking = new StringEater(socketMessage.Content);
                while (nameChecking.Count() > 0) {
                    string word = nameChecking.Get();
                    foreach (var kvp in memory.users) {
                        Memory.User u = kvp.Value;
                        if (u.username.ToLower() == word) {
                            messageTarget = u.username;
                        } else {
                            if (u.nickname != null) {
                                if (u.nickname.ToLower() == word) {
                                    messageTarget = u.username;
                                }
                            } else {
                                //that user had no nickname to compare against
                            }
                        }
                    }
                }
            }
            if (messageTarget == null) {
                //well let's see if they said _part_ of someone's name?
                //maybe someone who is online and has been chatting recently?
            }

            //todo: if both these methods failed, check message history and make a guess based on time and proximity.
            return messageTarget;
        }

        public string Unpronounify (string message, Memory.User messageAuthor) {
            //now use a stringEater to switch out pronouns in the message for the things they stand in for.
            string newMessage = "";
            StringEater pronounSwitcher = new StringEater(message);
            while (pronounSwitcher.Count() > 0) {
                string newWord;
                string word = pronounSwitcher.Get();
                switch (word) {
                    case "i":
                    case "me":
                        newWord = messageAuthor.username;
                        break;
                    case "my":
                        newWord = messageAuthor.username + "'s";
                        break;
                    case "i'm":
                    case "im":
                        newWord = messageAuthor.username + " is";
                        break;
                    case "you":
                        newWord = messageAuthor.speakingTo;
                        break;
                    default:
                        newWord = word;
                        break;
                }
                newMessage += newWord + " ";
            }
            return newMessage;
        }
    }

    public class Memory {
        Program program; //reference to the bot
        Storer storer; //reference to the bot's stats storer
        Statistics data; //reference to bot's data
        SocketTextChannel log; //logging channel
        SocketGuild guild;
        DiscordSocketClient client;
        
        public Dictionary<string, Word> words;
        public Dictionary<string, Concept> concepts;
        public Dictionary<string, User> users;
        public enum wordActions { concept, equate, contain };

        public Memory() {
            //frontload nonon with a basic understanding of english.
            words = new Dictionary<string, Word>() {
                { "is",new Word(wordActions.equate) },
                { "are",new Word(wordActions.equate) },
                { "have",new Word(wordActions.contain) },
            };

            concepts = new Dictionary<string, Concept>() { };

            users = new Dictionary<string, User>() { };            
        }

        public void AddUser(SocketGuildUser u) {
            concepts.Add(u.Username,new Concept());
            users.Add(u.Username,new User(concepts[u.Username],u));
        }

        public class User {
            //keep track of present information about discord users to better navigate conversations.
            //such as names, and conversation direction.
            public Concept concept; //relevant concept (information we remember about this user)
            public string username;
            public string mention;
            public string nickname;
            public string freeName;

            public string speakingTo = null;
            public DateTime lastSpoke = DateTime.MinValue;

            public User(Concept c,SocketGuildUser u) {
                concept = c;
                username = u.Username;
                mention = u.Mention;
                nickname = u.Nickname;
            }
        }

        public class Word {
            wordActions action;

            public Word(wordActions a) {
                action = a;
            }
        }

        public class Concept {
            string type;
            List<Concept> contains;
            List<Concept> containedBy;
            List<Concept> properties;
            List<Concept> propertyOf;
            List<Concept> similarTo;
        }
    }
}