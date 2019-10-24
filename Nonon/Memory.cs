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
        Queue<Memory.MessageContainer> pastMessages = new Queue<Memory.MessageContainer>(); //used for keeping track of conversation direction

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
            //interpret a message and gain a variety of information about it.
            //create a MessageContainer class to store the socketMessage and all of our metadata we found when parsing.
            Memory.MessageContainer thisMessage = new Memory.MessageContainer(socketMessage);
            
            //if the queue is too long lose the last item.
            if (pastMessages.Count > 10) {
                pastMessages.Dequeue();
            }
            string message = socketMessage.Content.ToLower();
            string output = "";
            output += "There are" + pastMessages.Count + " Past Messages\n";
            //TASK 1: Recognize the message author.
            //If there is no discord user associated with this message yet, add one.
            if (!memory.users.ContainsKey(socketMessage.Author.Username)) { memory.AddUser(socketMessage.Author); }
            //recall who this is from memory.
            Memory.User messageAuthor = memory.users[socketMessage.Author.Username];
            //TASK 2: Figure out who the message is directed at.
            Memory.User messageTarget = FindMessageTarget(socketMessage);
            //remember this message's target.
            thisMessage.messageTarget = messageTarget;
            output += "\nThis message's target is: " + (messageTarget == null ? "null" : messageTarget.username);
            //update the author's speakingTo for this moment.
            messageAuthor.speakingTo = messageTarget;
            output += "\nMessage author " + messageAuthor.username + " is currently speaking to " + (messageTarget == null ? "nobody" : messageAuthor.speakingTo.username);
            message = Unpronounify(message, messageAuthor);

            //remember that this message occurred.
            pastMessages.Enqueue(thisMessage);
            program.Say(log, output);
        }

        public Memory.User FindMessageTarget(SocketMessage socketMessage) {
            string output = "";
            Memory.User messageTarget = null;
            string messageTargetUsername = null;
            string authorName = socketMessage.Author.Username;
            Int32 now = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            //try to parse who they are speaking to.
            if (socketMessage.MentionedUsers.Count > 0) {
                //if they mentioned someone then that's pretty easy.
                foreach (SocketUser mentionedUser in socketMessage.MentionedUsers) {
                    if (mentionedUser.Username != authorName) { //but obviously they arent talking to themselves probably.
                        messageTargetUsername = mentionedUser.Username;
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
                            messageTargetUsername = u.username;
                        } else {
                            //this wasn't a username...
                            if (u.nickname != null && u.nickname.ToLower() == word) {
                                messageTargetUsername = u.username;
                            } else {
                                //this wasn't a nickname...
                                if (u.freeName != null && u.freeName.ToLower() == word) {
                                    messageTargetUsername = u.freeName;
                                } else {
                                    //this wasn't a free name either. darn.
                                }
                            }
                        }
                    }
                }
            }
            if (messageTarget == null) {
                if (pastMessages.Count > 0) { //can't do this without any past messages you know
                    //okay well if we still don't know who this message was directed to, let's go through the previous message targets.
                    Queue<Memory.MessageContainer> relevantMessages = pastMessages;
                    //run through the relevantMessages and 
                    // - ignore any that are older then, say, 20 minutes
                    // - ignore any that have a null messageTarget
                    while (relevantMessages.Count > 0) {
                        Memory.MessageContainer messageContainer = relevantMessages.Dequeue();
                        output += now + " - " + messageContainer.timeStamp + " = " + (now - messageContainer.timeStamp) + " > 1200\n";
                        //update the messageTarget if every condition is met
                        if (now - messageContainer.timeStamp < 1200) {
                            if (messageContainer.messageTarget != null) {
                                messageTargetUsername = messageContainer.messageTarget.username;
                            }
                        }
                    }
                }
            }
            //so now we likely have some kind of identifying key, typically a username. Try to remember who this username belongs to.
            //if we dont recognize the username, then return a null.
            if (messageTargetUsername != null) {
                if (memory.users.ContainsKey(messageTargetUsername)) { messageTarget = memory.users[messageTargetUsername]; }
            }
            program.Say(log, output);
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
                        newWord = messageAuthor.speakingTo.username;
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

        public void AddUser(SocketUser u) {
            concepts.Add(u.Username, new Concept());
            users.Add(u.Username, new User(concepts[u.Username], u));
        }

        public class User {
            //keep track of present information about discord users to better navigate conversations.
            //such as names, and conversation direction.
            public Concept concept; //relevant concept (information we remember about this user)
            public SocketUser socketUser;
            public string username;
            public string mention;
            public string nickname;
            public string freeName;

            public User speakingTo = null;
            public DateTime lastSpoke = DateTime.MinValue;

            public User(Concept c, SocketUser u) {
                concept = c;
                socketUser = u;
                username = u.Username;
                mention = u.Mention;
                nickname = null;
                try {
                    SocketGuildUser gu = u as SocketGuildUser;
                    nickname = gu.Nickname;
                } catch {}
                GenerateFreeName();
            }

            public void GenerateFreeName() {
                //collect all the words in someone's name.
                string bestName = nickname == null ? username : nickname;
                Console.WriteLine("testing for: " + bestName);
                //remove punctuation and emoji from string
                bestName = new string(bestName.Where(c => (char.IsLetter(c) || char.IsWhiteSpace(c))).ToArray());
                string[] tokens = bestName.Split(' ');
                //build a set of scores for each subname.
                Dictionary<string, int> subNames = new Dictionary<string, int>();
                int i = 0, score = 0;
                foreach (string token in tokens) {
                    if (token.Length > 0) { //don't add empty words
                        score = 0;
                        //score by word length. divide by two so that similar lengths score the same. 
                        score += token.Length / 2;
                        //score higher if the word appears in the person's username.
                        if (username.ToLower().Contains(token.ToLower())) {
                            score += 10;
                        }
                        //score higher if the word occurs first in the string.
                        score += (tokens.Length - i);
                        if (!subNames.ContainsKey(token)) { subNames.Add(token, score); }
                        i++;
                    }
                }
                //tokenize the username and also add that into the subName list.
                //this is to cover the case where someone's name is incomprehensible (eg, if it's made entirely of non-ascii characters)
                //and it should use their username instead.
                tokens = username.Split(' ');
                foreach (string token in tokens) {
                    if (token.Length > 0) {
                        if (!subNames.ContainsKey(token)) { subNames.Add(token, 0); }
                    }
                }
                //todo: currently there is no fallback if their username is also incomprehensible. discord allows non ascii characters in usernames.
                //choose the highest scoring token.
                score = -1;
                foreach (var subName in subNames) {
                    Console.WriteLine(subName.Key + ", " + subName.Value);
                    if (subName.Value > score) {
                        score = subName.Value;
                        freeName = subName.Key;
                    }
                }
                Console.WriteLine("free name:" + freeName);
            }

            public string ToString() {
                return username;
            }
        }

        public class MessageContainer {
            //stores the message metadata with the socketMessage
            public SocketMessage socketMessage;
            public User messageTarget = null; //the person this message was most likely directed at.
            public Int32 timeStamp;

            public MessageContainer(SocketMessage sm) {
                socketMessage = sm;
                timeStamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
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