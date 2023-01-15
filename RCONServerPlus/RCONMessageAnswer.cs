using System;
using System.Text;

namespace RCONServerPlus
{
	internal sealed class RCONMessageAnswer
	{
		public static readonly RCONMessageAnswer EMPTY = new RCONMessageAnswer(false, new byte[] {});
		
		private readonly bool success = false;
		private readonly byte[] data = null;
		private readonly int responseId = -1;
		
		public RCONMessageAnswer(bool success, byte[] data, int responseId = -1)
		{
			this.success = success;
			this.data = data;
			this.responseId = responseId;
		}
		public byte[] Data
		{
			get
			{
				return data;
			}
		}
		public bool Success
		{
			get
			{
				return success;
			}
		}
		public string Answer
		{
			get
			{
				return Encoding.UTF8.GetString(data);
			}
		}
		public int ResponseId
		{
			get
			{
				return responseId;
			}
		}
	}
}
