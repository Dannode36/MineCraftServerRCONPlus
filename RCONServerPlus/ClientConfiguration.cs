using System;
using System.Collections.Generic;
using System.Text;

namespace RCONServerPlus
{
    public class ClientConfiguration
    {
        public static readonly ClientConfiguration DEFAULT = new ClientConfiguration();
        // Current servers like e.g. Spigot are not able to work async :(
        public bool rconServerIsMultiThreaded;
        public bool retryConnect;
        public int timeoutSeconds;
        public int reconnectDelaySeconds;
        public int reconnectAttempts;

        public ClientConfiguration()
        {
            rconServerIsMultiThreaded = false;
            retryConnect = true;
            timeoutSeconds = 3;
            reconnectDelaySeconds = 5;
            reconnectAttempts = 5;
        }
    }
}
