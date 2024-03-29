﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;


using TcpMonitoring;
using TcpMonitoring.MessagingObjects;
using TcpMonitoring.QueueingItems;
using System.Collections.Concurrent;

namespace TcpMonitorPublisher
{
	public class StateObject
	{
		// Client  socket.
		public Socket workSocket = null;
		// Size of receive buffer.
		public const int BufferSize = 1024;
		// Receive buffer.
		public byte[] buffer = new byte[BufferSize];
		// Received data string.
		public StringBuilder sb = new StringBuilder();

		public Type MessageType = null;
	}

	public enum PublisherClientState
	{
		Disconnected = 0,
		Initializing = 1,
		Connected = 2
	}

	public class TcpPublisherServer
	{
		private static readonly TcpPublisherServer _Instance = new TcpPublisherServer();
		private object _sendLock = new object();

		private TcpListener _monitorServer;
		public Socket clientSocket = null;
		public PublisherClientState clientState = PublisherClientState.Disconnected;

		public ConcurrentBag<QueueItemStateChangeMessage> itemsChangedInInitialization = new ConcurrentBag<QueueItemStateChangeMessage>();

		private TcpPublisherServer() { }

		public static TcpPublisherServer Instance => _Instance;

		public void StartServer(string ipStr, int port)
		{
			try
			{
				IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ipStr), port);
				_monitorServer = new TcpListener(endpoint);
				_monitorServer.Start();
				Console.WriteLine("Publisher Server running and waiting for clients.");
				WaitForClients();
			}
			catch (Exception)
			{

			}
		}

		// Connection calls;

		public void WaitForClients()
		{
			//_IncomingConnectionDone.WaitOne();
			Socket monitorClient = new Socket(SocketType.Stream, ProtocolType.Tcp);
			_monitorServer.BeginAcceptSocket(new AsyncCallback(OnClientConnected), monitorClient);
		}

		public void OnClientConnected(IAsyncResult result)
		{
			//_IncomingConnectionDone.Set();
			if (result.IsCompleted)
			{
				Console.WriteLine("Client Subscribed.");
				clientSocket = _monitorServer.EndAcceptSocket(result);
				WaitForClients();
				Receive();
				InitializeClient();
			}
			else
			{
				throw new Exception();
			}
		}

		private void InitializeClient()
		{
			MonitoringInitializationMessage queueItems = new MonitoringInitializationMessage(MockQueueItems.items.Values.ToList(), HeartBeatPublisher.ipAddress.ToString(), HeartBeatPublisher.Port);
			clientState = PublisherClientState.Initializing;


			// make use of concurrent bag
			SendAsync(queueItems);	
		}

		// Sending calls

		private void SendAsync(IMessage data, Type messageType = null)
		{
			if (clientSocket != null)
			{
				string msgJson = SerializeMonitorMessage(data);

				msgJson += "<EOM>";
				byte[] byteArr = Encoding.ASCII.GetBytes(msgJson);
				StateObject state = new StateObject();
				state.workSocket = clientSocket;

				switch (data)
				{
					
					case HeartbeatObject H:
					case MessageObject M:
					case QueueItemStateChangeMessage Q:
					case UnsubscribeMessageObject U:
					case ChangedItemsWhileInitializingMessage C:
						clientSocket.BeginSend(byteArr, 0, byteArr.Length, 0, new AsyncCallback(SendCallback), state);
						break;
					case MonitoringInitializationMessage M:	
						clientSocket.BeginSend(byteArr, 0, byteArr.Length, 0, new AsyncCallback(InitializationCallback), state);
						break;
					default:
						break;
				}
			}
			else
			{
				throw new Exception();
			}
		}

		private void InitializationCallback(IAsyncResult result)
		{
			if (result.IsCompleted)
			{
				//Socket client = (Socket)result.AsyncState;
				StateObject state = (StateObject)result.AsyncState;
				int bytesSent = state.workSocket.EndSend(result);

				if (itemsChangedInInitialization.Count > 0)
				{
					List<QueueItemStateChangeMessage> messagesList = itemsChangedInInitialization.ToList();
					SendAsync(new ChangedItemsWhileInitializingMessage() { items = messagesList });
				}

				clientState = PublisherClientState.Connected;
			}
			else
			{
				throw new Exception();
			}
		}

		private void SendCallback(IAsyncResult result)
		{
			if (result.IsCompleted)
			{
				//Socket client = (Socket)result.AsyncState;
				StateObject state = (StateObject)result.AsyncState;

				int bytesSent = state.workSocket.EndSend(result);
			}
			else
			{
				throw new Exception();
			}
		}

		private void SendChangedItemsInInitialization(ChangedItemsWhileInitializingMessage c)
		{

		}

		// Receiving calls.
		private void Receive()
		{
			try
			{
				if (clientSocket != null)
				{
					StateObject state = new StateObject();
					state.workSocket = clientSocket;

					clientSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			if (clientSocket.Connected && result.IsCompleted)
			{

				StateObject state = (StateObject)result.AsyncState;
				Socket handler = state.workSocket;
				int bytesRead = handler.EndReceive(result);
				state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
				string msg = state.sb.ToString();
				HandleReceivedMessage(msg);
			}
			else
			{
				throw new Exception();
			}
		}

		// Received Message Handling

		private void HandleReceivedMessage(string jsonMsg)
		{
			try
			{
				IMessage message = JsonConvert.DeserializeObject<IMessage>(jsonMsg, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All, Formatting = Formatting.Indented });

				switch (message)
				{
					case UnsubscribeMessageObject U:
						HandleUnsubscribeMessage(U);
						break;
					case ErrorMessageObject E:
						HandleErrorMessage(E);
						break;
					default:
						break;
				}
			}
			catch (Exception)
			{

			}
		}

		private void HandleErrorMessage(ErrorMessageObject e)
		{
			WorkerThreads.MockQueueItemsWorkers.Abort();
			Console.WriteLine("Worker thread stopped because: " + e.ErrorMessage);
		}

		private void HandleUnsubscribeMessage(IMessage msg)
		{
			Console.WriteLine("client unsubscribed");
			Close();
		}
		
		public void SendQueueItemStateChange(IMessage message)
		{
			try
			{
				SendAsync(message);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		private string SerializeMonitorMessage(IMessage message)
		{
			return JsonConvert.SerializeObject(message, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All, Formatting = Formatting.Indented });
		}

		public void Close()
		{
			clientState = PublisherClientState.Disconnected;
			clientSocket.Close();
		}
	}
}
