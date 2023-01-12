# RCONServerPlus
A thread-safe Minecraft server's RCON implementation for C# rewritten in .NET Standard 2.0. Based on [MinecraftServerRCONSharp](https://github.com/ShineSmile/MinecraftServerRCON)

## Issues or Bugs
If you come across any bugs or issues of the sort (even ideas for things I could add) please feel free to open a new issue outlining your problem :)

## Setup
Easiest way to use this library is via [NuGet](https://www.nuget.org/packages/RCONServerPlus)

Or download the library from the [releases](https://github.com/Dannode36/MinecraftServerRCONPlus/releases) page.

### Example usage: Change the gamemode of the player "Steve" to creative
```C#
using MinecraftServerRCON;

class RCONTest
{
    static void Main(string[] args)
    {
        using var rcon = new RCONClient();
        rcon.SetupStream("127.0.0.1", 25575, password: "123", tryReconnect: true);
        string answer = rcon.SendMessage(RCONMessageType.Command, "gamemode creative Steve");
        Console.WriteLine(answer.RemoveColorCodes());
    }
}
```
