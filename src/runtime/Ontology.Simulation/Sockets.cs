namespace Ontology.Simulation
{
	public class Sockets
	{
		public class Message
		{
			public int MessageID;
			public Prototype MessageValue;
			public Prototype Response;
			public Semaphore Semaphore;
		}

		private List<Message> m_Messages = new List<Message>();
		private object m_Lock = new object();

		public Prototype SendMessageAndWaitForReply(Prototype prototype)
		{
			Message message = new Message();

			lock (m_Lock)
			{
				message.MessageID = m_Messages.Count + 1;
				message.MessageValue = prototype;
				message.Semaphore = new Semaphore(0, 1);

				m_Messages.Add(message);				
			}

			message.Semaphore.WaitOne();

			return message.Response;
		}

		public void Respond(int iMessageID, Prototype prototype)
		{
			Message? message = null;

			lock (m_Lock)
			{
				message = m_Messages.FirstOrDefault(x => x.MessageID == iMessageID);
			}

			message.Response = prototype;
			message.Semaphore.Release();
		}

		public Message GetNextMessage(int iLastMessageID)
		{
			if (m_Messages.Count > iLastMessageID)
				return m_Messages[iLastMessageID];

			return null;
		}
	}
}
