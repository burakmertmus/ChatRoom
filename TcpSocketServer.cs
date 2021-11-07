using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatRoom
{
    class TcpSocketServer
    {

        // What listens in
        private TcpListener _listener;

        private List<TcpClient> _messengers = new List<TcpClient>();

        // Names that are taken by other messengers
        private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();

        // Messages that need to be sent
        private Queue<string> _messageQueue = new Queue<string>();

        // Extra fun data
        public readonly string ChatName;
        public readonly int Port;
        public bool Running { get; private set; }

        // Buffer
        public readonly int BufferSize = 2 * 1024;  // 2KB

        // Make a new TCP chat server, with our provided name
        public TcpSocketServer(string chatName, int port)
        {
            // Set the basic data
            ChatName = chatName;
            Port = port;
            Running = false;

            // Make the listener listen for connections on any network device
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        // If the server is running, this will shut down the server
        public void Shutdown()
        {
            Running = false;
            Console.WriteLine("Shutting down server");
        }

        // Start running the server.  Will stop when `Shutdown()` has been called
        public void Run()
        {
            // Some info
            Console.WriteLine("Starting the \"{0}\" TCP Chat Server on port {1}.", ChatName, Port);
            Console.WriteLine("Press Ctrl-C to shut down the server at any time.");

            // Server start
            _listener.Start();           
            Running = true;

            // Main server loop
            Console.WriteLine("Runnging");
            while (Running)
            {
                // Check for new clients
                if (_listener.Pending()) {
                    _handleNewConnection();
                }


                // Do the rest
                _checkForDisconnects();

                
                
                try
                {
                    _sendMessages();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                _checkForNewMessages();
                // Use less CPU
                Thread.Sleep(10);

            }


            // Stop the server, and clean up any connected clients
            foreach (TcpClient m in _messengers)
            {

                _cleanupClient(m);
            }
            _listener.Stop();

            // Closeing message
            Console.WriteLine("Server is shut down.");
        }

        private void _handleNewConnection()
        {
            // There is (at least) one, see what they want
            bool good = false;
            TcpClient newClient = _listener.AcceptTcpClient();      // Blocks
            NetworkStream netStream = newClient.GetStream();

            // Modify buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine("Handling a new client from {0}...", endPoint);

            //Print Client for get user message
            string message = "Please Enter Your User Name:";
            byte[] _dataName = Encoding.UTF8.GetBytes(message);
            netStream.Write(_dataName, 0, _dataName.Length);

            // Let them identify themselves
            byte[] msgBuffer = new byte[BufferSize];

            int bytesRead = netStream.Read(msgBuffer, 0, msgBuffer.Length);     // Blocks

            if (bytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);

                if (msg.StartsWith(""))
                {

                    var mesg1=msg.Split(':');
                    string name= mesg1[0];
                    if ((name != string.Empty) && (!_names.ContainsValue(name)))
                    {     
                        // They're new here, add them in
                        good = true;
                        _names.Add(newClient, name);
                        _messengers.Add(newClient);

                        Console.WriteLine("{0} is a Messenger with the name {1}.", endPoint, name);
                        // Tell the we have a new messenger

                        _sendChat(newClient);


                        _messageQueue.Enqueue(String.Format("{1} : {0} has joined the chat.", name, "System"));
                        ChatQueue.Add(String.Format("{1} : {0} has joined the chat.", name, "System"));
                    }
                }
                else
                {
                    Console.WriteLine("Error!", endPoint);
                    _cleanupClient(newClient);
                }
            }

            if (!good)
                newClient.Close();
        }

        // Check For Anyone leaved server
        private void _checkForDisconnects()
        {
          

            // Check the messengers second
            foreach (TcpClient m in _messengers.ToArray())
            {
                if (_isDisconnected(m))
                {
                    // Get info about the messenger
                    string name = _names[m];

                    // Tell the someone has left
                    Console.WriteLine("Messeger {0} has left.", name);
                    _messageQueue.Enqueue(String.Format("{0} has left the chat", name));
                    ChatQueue.Add(String.Format("{1} : {0} has left.", m.Client.RemoteEndPoint, "System"));
                    // clean up on our end 
                    _messengers.Remove(m);  // Remove from list
                    _names.Remove(m);       // Remove taken name
                    _cleanupClient(m);
                }
            }
        }

            private void _sendChat(TcpClient m)
            {
                foreach (string msg in ChatQueue.Get())
                {
                    // Encode the message
                    byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
                    m.GetStream().Write(msgBuffer, 0, msgBuffer.Length);
                    
                }
            }


            // Check For New Message
            private void _checkForNewMessages()
            {
                foreach (TcpClient m in _messengers)
                {
                
                int messageLength = m.Available;
                
                    if (messageLength > 0)
                    {
                        // there is one!  get it
                        byte[] msgBuffer = new byte[messageLength];
                        m.GetStream().Read(msgBuffer, 0, msgBuffer.Length); 
                        // Attach a name to it and shove it into the queue
                        string msg = String.Format("{0}: {1}", _names[m],Encoding.UTF8.GetString(msgBuffer));
                        _messageQueue.Enqueue(msg);
                        ChatQueue.Add(msg);
                }
                }
            }

            // Clears out the message queue (and sends it to all of users)
            private void _sendMessages()
            {

                foreach (string msg in _messageQueue)
                {
                    // Encode the message
                    byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                // Send the message to each messengers
                foreach (TcpClient v in _messengers)
                    {
                        v.GetStream().Write(msgBuffer, 0, msgBuffer.Length);
                        // Blocks
                    }

                }

                // clear out the queue
                _messageQueue.Clear();
            }

            
            // Checks if a socket has disconnected
            private static bool _isDisconnected(TcpClient client)
            {
                try
                {
                    Socket s = client.Client;
                    return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
                }
                catch (SocketException se)
                {
                    // We got a socket error, assume it's disconnected
                    return true;
                }
            }

            // cleans up resources for a TcpClient
            private static void _cleanupClient(TcpClient client)
            {
                client.GetStream().Close();     // Close network stream
                client.Close();                 // Close client
            }

            public static TcpSocketServer chat;

            protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
            {
                chat.Shutdown();
                args.Cancel = true;
            }

            public static void Main(string[] args)
            {
                // Create the server
                string name = "Letta ChatRoom";
                int port = 5353;
                chat = new TcpSocketServer(name, port);

                // Add a handler for a Ctrl-C press
                Console.CancelKeyPress += InterruptHandler;

                // run the chat server
                chat.Run();
            }
    }
    
}
