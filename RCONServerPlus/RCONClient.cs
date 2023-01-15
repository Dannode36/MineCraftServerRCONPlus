using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MinecraftServerRCON
{
	public sealed class RCONClient : IDisposable
	{
		public bool IsConnected
        {
			get
			{
                return tcp.Connected;
            }
        }
        public bool IsInit
        {
			get
			{
				return isInit;
			}
        }
        public bool IsConfigured
		{
			get
			{
                return isConfigured;
            }
        }
		public bool tryReconnect;

		// Current servers like e.g. Spigot are not able to work async :(
		private readonly bool rconServerIsMultiThreaded = false;
		private int timeoutSeconds;
        private int reconnectDelaySeconds;
        private int maxReconAttempts;
        private int curReconAttempts;
		private static readonly byte[] PADDING = new byte[] { 0x0, 0x0 };
		private bool isInit = false;
		private bool isConfigured = false;
		private string server = string.Empty;
		private string password = string.Empty;
		private int port = 25575;
		private int messageCounter = 0;
		private NetworkStream stream = null;
		private TcpClient tcp = null;
		private BinaryWriter writer = null;
		private BinaryReader reader = null;
		private ReaderWriterLockSlim threadLock = new ReaderWriterLockSlim();
		private RCONReader rconReader = RCONReader.INSTANCE;

        public RCONClient()
		{
			isInit = false;
			isConfigured = false;
		}

        /// <summary>
        /// Avoid trying to connect to the same server from multiple clients as it could result in an AutshException.
        /// </summary>
        /// <param name="server">IP of the server</param>
        /// <param name="port">RCON port (defaults to 25575 for minecraft)</param>
        /// <param name="password">The password configured in server.properties</param>
        /// <param name="timeoutSeconds">How long to wait for a response (anything less than 0 is not recommended as the server may never respond)</param>
        /// <param name="retryConnect">Retry initial connection to the server if it fails</param>
        /// <param name="retryDelaySeconds">Time between reconnection attempts</param>
        /// <returns></returns>
        public RCONClient SetupStream(string server = "127.0.0.1", int port = 25575, string password = "", int timeoutSeconds = 3, bool tryReconnect = false, int reconnectDelaySeconds = 5, int maxReconAttempts = 10)
		{
			threadLock.EnterWriteLock();

			try
			{
				if (isConfigured)
				{
					return this;
				}

				this.server = server;
				this.port = port;
				this.password = password;
				isConfigured = true;
				this.timeoutSeconds = timeoutSeconds;

				this.tryReconnect = tryReconnect;
				this.reconnectDelaySeconds = reconnectDelaySeconds;
				this.maxReconAttempts = maxReconAttempts;
				curReconAttempts = 0;

				OpenConnection();
				return this;
			}
			finally
			{
				threadLock.ExitWriteLock();
			}
		}

		public string SendMessage(RCONMessageType type, string command)
		{
			if (!isConfigured)
			{
				return RCONMessageAnswer.EMPTY.Answer;
			}

			return InternalSendMessage(type, command).Answer;
		}

		public void FireAndForgetMessage(RCONMessageType type, string command)
		{
			if (!isConfigured)
			{
				return;
			}

			InternalSendMessage(type, command, true);
		}

		private void OpenConnection()
		{
			if (isInit)
			{
				return;
			}

			try
			{
				rconReader = RCONReader.INSTANCE;
				tcp = new TcpClient(server, port);
				stream = tcp.GetStream();
				writer = new BinaryWriter(stream);
				reader = new BinaryReader(stream);
				rconReader.setup(reader);

				if (password != string.Empty)
				{
					var answer = InternalSendAuth();

					//Response ID of -1 means auth failed which EMPTY defaults to
					if (answer == RCONMessageAnswer.EMPTY)
					{
						isInit = false;
						throw new AuthException("Authentication failed (check password)");
					}
				}

				isInit = true;
			}
			catch(AuthException ex)
			{
                isInit = false;
                isConfigured = false;
                Console.Error.WriteLine("Exception while connecting: " + ex.Message);

				//Only say this if there are reconnection attempts remaining
				if(curReconAttempts != maxReconAttempts)
				{
					Console.Error.WriteLine("Reconnection will not be attempted due to this error");
				}
            }
			catch(Exception ex)
			{
                isInit = false;
                isConfigured = false;
                Console.Error.WriteLine("Exception while connecting: " + ex.Message);
				if (tryReconnect)
				{
                    if (curReconAttempts < maxReconAttempts)
                    {
                        curReconAttempts++;
                        Console.Error.WriteLine($"Attempting reconnect in {reconnectDelaySeconds} seconds [{curReconAttempts}/{maxReconAttempts}]");
                        Thread.Sleep(TimeSpan.FromSeconds(reconnectDelaySeconds));
                        OpenConnection();
                    }
                    else
                    {
                        curReconAttempts = 0;
                    }
                }
            }
            finally
            {
                // To prevent huge CPU load if many reconnects happens.
                // Does not effect any normal case ;-)
                Thread.Sleep(TimeSpan.FromSeconds(0.1));
            }
        }

		private RCONMessageAnswer InternalSendAuth()
		{
			// Build the message:
			var command = password;
			var type = RCONMessageType.Login;
			var messageNumber = ++messageCounter;
			var msg = new List<byte>();
			msg.AddRange(BitConverter.GetBytes(10 + Encoding.UTF8.GetByteCount(command)));
            msg.AddRange(BitConverter.GetBytes(messageNumber));
			msg.AddRange(BitConverter.GetBytes((int)type));
			msg.AddRange(Encoding.UTF8.GetBytes(command));
			msg.AddRange(PADDING);

			// Write the message to the wire:
			writer.Write(msg.ToArray());
			writer.Flush();

			return WaitReadMessage(messageNumber);
		}

		private RCONMessageAnswer InternalSendMessage(RCONMessageType type, string command, bool fireAndForget = false)
		{
			try
			{
				var messageNumber = 0;

				try
				{
					threadLock.EnterWriteLock();

					// Is a reconnection necessary?
					if (!isInit || tcp == null || !tcp.Connected)
					{
						InternalDispose();
						OpenConnection();
					}


                    // Build the message:
                    messageNumber = ++messageCounter;
					var msg = new List<byte>();
					msg.AddRange(BitConverter.GetBytes(10 + Encoding.UTF8.GetByteCount(command)));
					msg.AddRange(BitConverter.GetBytes(messageNumber));
					msg.AddRange(BitConverter.GetBytes((int)type));
					msg.AddRange(Encoding.UTF8.GetBytes(command));
					msg.AddRange(PADDING);

					// Write the message to the wire:
					writer.Write(msg.ToArray());
					writer.Flush();
				}
				finally
				{
					threadLock.ExitWriteLock();
				}

				if (fireAndForget && rconServerIsMultiThreaded)
				{
					var id = messageNumber;
					Task.Factory.StartNew(() =>
					{
						WaitReadMessage(id);
					});

					return RCONMessageAnswer.EMPTY;
				}

				return WaitReadMessage(messageNumber);
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception while sending: " + e.Message);
				return RCONMessageAnswer.EMPTY;
			}
		}

		private RCONMessageAnswer WaitReadMessage(int messageNo)
		{
			var sendTime = DateTime.UtcNow;
			while (true)
			{
				var answer = rconReader.getAnswer(messageNo);
				if (answer == RCONMessageAnswer.EMPTY)
				{
					//If timeoutSeconds is negative keep trying
					if (timeoutSeconds > 0 && (DateTime.UtcNow - sendTime).TotalSeconds > timeoutSeconds)
					{
						return RCONMessageAnswer.EMPTY;
					}

					Thread.Sleep(TimeSpan.FromSeconds(0.001));
					continue;
				}

				return answer;
			}
		}

		#region IDisposable implementation
		public void Dispose()
		{
			threadLock.EnterWriteLock();

			try
			{
				InternalDispose();
			}
			finally
			{
				threadLock.ExitWriteLock();
			}
		}
        private void InternalDispose()
        {
            isInit = false;

            try
            {
                rconReader.Dispose();
            }
            catch
            {
            }

            if (writer != null)
            {
                try
                {
                    writer.Dispose();
                }
                catch
                {
                }
            }

            if (reader != null)
            {
                try
                {
                    reader.Dispose();
                }
                catch
                {
                }
            }

            if (stream != null)
            {
                try
                {
                    stream.Dispose();
                }
                catch
                {
                }
            }

            if (tcp != null)
            {
                try
                {
                    tcp.Close();
                }
                catch
                {
                }
            }
        }
        #endregion
    }
}
