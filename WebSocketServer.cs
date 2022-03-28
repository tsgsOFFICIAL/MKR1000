using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ArduinoApiHTTP
{
    /// <summary>
    /// The WebSocketServer handles a connection via a websocket, a client can be used for connecting.
    /// </summary>
    internal class WebSocketServer
    {
        /// <summary>
        /// The IP of the server
        /// </summary>
        public string Ip { get; private set; }
        /// <summary>
        /// The Port of the server
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// The TcpListener / WebSocketServer
        /// </summary>
        private TcpListener _listener;
        /// <summary>
        /// Run condition
        /// </summary>
        public bool Running { get; private set; }
        /// <summary>
        /// Thread counter
        /// </summary>
        private int _threadCounter = 0;
        /// <summary>
        /// The lock
        /// </summary>
        private object _lock = new object();
        private TcpClient _lastClient = new TcpClient();

        /// <summary>
        /// Creates a new instance of the WebSocketServer class
        /// </summary>
        public WebSocketServer()
        {
            Ip = "192.168.2.103";
            Port = 8008;
            _listener = new TcpListener(IPAddress.Parse(Ip), Port);
        }

        private void UpdateUI(object sender, string e)
        {
            SendMessageToClient(_lastClient, e);
        }

        /// <summary>
        /// Starts the listener
        /// </summary>
        /// <returns>This method returns a boolean value indicating the success state of the method call</returns>
        public async void Open()
        {
            try
            {
                _listener.Start();
                Running = true;

                EventManager.ApiPosted += UpdateUI;

                while (Running)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    await Task.Run(() =>
                    {
                        new Thread(new ParameterizedThreadStart(HandleClient!)).Start(client);
                        lock (_lock)
                        {
                            Console.WriteLine($"A client connected {++_threadCounter} threads active");
                        }

                    });
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"An error occured: \n\n{ex}");
            }
        }
        /// <summary>
        /// Stops the listener
        /// </summary>
        /// <returns>This method returns a boolean value indicating the success state of the method call</returns>
        public bool Close(bool restart = false)
        {
            try
            {
                _listener.Stop();
                Running = false;

                if (restart)
                {
                    Open();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// Runs the websocket
        /// </summary>
        public void HandleClient(object obj)
        {
            _lastClient = (TcpClient)obj;

            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();

            while (true)
            {
                while (!stream.DataAvailable) ;
                while (client.Available < 3) ; // Match against "get"

                byte[] bytes = new byte[client.Available];
                stream.Read(bytes, 0, client.Available);
                string s = Encoding.UTF8.GetString(bytes);

                if (bytes.Length == 8 && bytes[0] == 136 && bytes[1] == 130)
                {
                    break;
                }

                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    Console.WriteLine($"\n=====Handshaking from client=====\n\n{s}");

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                    string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    bool fin = (bytes[0] & 0b10000000) != 0,
                        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                        msglen = bytes[1] - 128, // & 0111 1111
                        offset = 2;

                    if (msglen == 126)
                    {
                        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (mask)
                    {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (int i = 0; i < msglen; ++i)
                            decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        string text = Encoding.UTF8.GetString(decoded);

                        Console.WriteLine($"Message Recieved: \"{text}\"");
                        SendMessageToClient(client, text);
                    }
                    else
                    {
                        Console.WriteLine("mask bit not set");
                    }

                    Console.WriteLine();
                }
            }

            lock (_lock)
            {
                Console.WriteLine($"Connection closed {--_threadCounter} threads active");
            }
        }
        public void SendMessageToClient(TcpClient client, string msg)
        {
            client = client ?? _lastClient;

            NetworkStream stream = client.GetStream();
            Queue<string> que = new Queue<string>(msg.SplitInGroups(125));
            int len = que.Count;

            while (que.Count > 0)
            {
                int header = GetHeader(
                    que.Count > 1 ? false : true,
                    que.Count == len ? false : true
                );

                byte[] list = Encoding.UTF8.GetBytes(que.Dequeue());
                header = (header << 7) + list.Length;
                stream.Write(IntToByteArray((ushort)header), 0, 2);
                stream.Write(list, 0, list.Length);
            }
        }


        protected int GetHeader(bool finalFrame, bool contFrame)
        {
            int header = finalFrame ? 1 : 0;//fin: 0 = more frames, 1 = final frame
            header = (header << 1) + 0;//rsv1
            header = (header << 1) + 0;//rsv2
            header = (header << 1) + 0;//rsv3
            header = (header << 4) + (contFrame ? 0 : 1);//opcode : 0 = continuation frame, 1 = text
            header = (header << 1) + 0;//mask: server -> client = no mask

            return header;
        }


        protected byte[] IntToByteArray(ushort value)
        {
            var ary = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(ary);
            }

            return ary;
        }
    }

    /// ================= [ extension class ]==============>
    public static class XLExtensions
    {
        public static IEnumerable<string> SplitInGroups(this string original, int size)
        {
            var p = 0;
            var l = original.Length;
            while (l - p > size)
            {
                yield return original.Substring(p, size);
                p += size;
            }
            yield return original.Substring(p);
        }
    }
}