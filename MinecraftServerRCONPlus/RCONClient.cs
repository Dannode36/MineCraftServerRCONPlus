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

		// Current servers like e.g. Spigot are not able to work async :(
		private readonly bool rconServerIsMultiThreaded = false;
		private int timeoutSeconds;
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

		public RCONClient SetupStream(string server = "127.0.0.1", int port = 25575, string password = "", int timeoutSeconds = 3)
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
					if (answer == RCONMessageAnswer.EMPTY)
					{
						isInit = false;
						throw new Exception("IPAddress or Password error!");
					}
				}

				isInit = true;
			}
            catch (Exception ex)
            {
                Console.WriteLine("Exception while connecting: " + ex.Message);
                isInit = false;
                isConfigured = false;
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
