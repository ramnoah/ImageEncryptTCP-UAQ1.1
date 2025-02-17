﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ImageEncryptTCP.Events;
using System.Threading;

namespace ImageEncryptTCP.Manager
{
    public class ConnectionManager
	{
		private static ConnectionManager _instance = new ConnectionManager();
		public static ConnectionManager Instance { get { return _instance; } }
		public string? ToIPAddress { get; set; }
		public string? Port { get; set; }
		public string? ImagePath { get; set; }
		public string? EncryptKey { get; set; }
		public bool IsActive { get; set; }

		public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
		public event EventHandler<ConnectionTimedOutEventArgs>? ConnectionTimedOut;

		private CancellationTokenSource cancellationTokenSource;

		private ConnectionManager() 
		{
			IsActive = true;
		}

		public void StartClient()
		{
			if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
			{
				Console.WriteLine("Client is already running.");
				HandleConnectionStatusChange(true);
				HandleTimedOutStatusChange(true);
				
				return;
			}

			cancellationTokenSource = new CancellationTokenSource();
			Task.Run(() => StartClientAsync(cancellationTokenSource.Token));
		}

		public void StopClient()
		{
			cancellationTokenSource?.Cancel();
		}

		private async Task StartClientAsync(CancellationToken cancellationToken)
		{
			try
			{
				if (Instance.ToIPAddress == null)
				{
					throw new Exception("An IP Address is needed.");
				}

				if (Instance.Port == null)
				{
					throw new Exception("A port is needed.");
				}

				using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(Instance.ToIPAddress), int.Parse(Instance.Port));

					Console.WriteLine("Conectando al servidor...");
					await client.ConnectAsync(remoteEP);
					Console.WriteLine("Conectado al servidor {0}", remoteEP);

					while (!cancellationToken.IsCancellationRequested)
					{
						Instance.SendData(client);
					}

					client.Shutdown(SocketShutdown.Both);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private void SendData(Socket client)
		{
			if (!string.IsNullOrEmpty(Instance.ImagePath))
			{
				byte[] img = File.ReadAllBytes(Instance.ImagePath);
				byte[] keyb = Encoding.ASCII.GetBytes(Instance.EncryptKey!);

				var encryptedImg = EncryptionManager.EncryptImage(Instance.ImagePath);
				List<char> imgb = encryptedImg.Select(i => (char)i).ToList();
				byte[] imgEb = Encoding.ASCII.GetBytes(imgb.ToArray());

				byte[] combinedData = new byte[sizeof(int) + imgEb.Length + keyb.Length];
				BitConverter.GetBytes(imgEb.Length).CopyTo(combinedData, 0);
				imgEb.CopyTo(combinedData, sizeof(int));
				keyb.CopyTo(combinedData, sizeof(int) + imgEb.Length);
				client.Send(combinedData);

				Console.WriteLine("Datos enviados");
				IsActive = false;
			}
		}

		private void OnConnectionChanged(ConnectionChangedEventArgs e)
		{
			ConnectionChanged?.Invoke(this, e);
		}

		private void OnTimedOutChanged(ConnectionTimedOutEventArgs e)
		{
			ConnectionTimedOut?.Invoke(this, e);
		}

		private void HandleConnectionStatusChange(bool isConnected) => OnConnectionChanged(new ConnectionChangedEventArgs(isConnected));
		private void HandleTimedOutStatusChange(bool isTimedOut) => OnTimedOutChanged(new ConnectionTimedOutEventArgs(isTimedOut));
	}
}
