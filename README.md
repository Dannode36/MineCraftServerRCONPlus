# MinecraftServerRCONSharp
A thread-safe Minecraft server's RCON implementation for C#. Original library [MinecraftServerRCONSharp](https://github.com/ShineSmile/MinecraftServerRCON) by ShineSmile

Example usage: Change the gamemode of the player "Steve" to creative
```C#
using MinecraftServerRCON;

class RCONTest
{
    static void Main(string[] args)
    {
        using var rcon = new RCONClient();
        rcon.SetupStream("127.0.0.1", 25575, password: "123");
        string answer = rcon.SendMessage(RCONMessageType.Command, "gamemode creative Steve");
        Console.WriteLine(answer.RemoveColorCodes());
    }
}
```
##

## Setup
Download the library from the [releases](https://github.com/Dannode36/MinecraftServerRCONPlus/releases) page.

**Nuget package coming soon**
