# Nonon
Nonon is a chat bot and stats bot for Discord. It is WIP.

It tracks message statistics, so you can check interesting things like which channels are the busiest or which users talk the most. It tallies the count of things, not their contents. It does not store your messages in any capacity, merely counts them when they happen.

I am still building it and parts are missing or might break. It currently logs messages that it sees, not previous chat history.

View the code in Program.cs.
Classes: 
- Program - Operates the Discord gateway and responds to network events.
- Data - Contains the currently known statistics stored as objects. Note that there is no message object or message contents.
- Storer - Operates the saving and loading of the data to JSON.
- StringEater - utility class used to process commands.
