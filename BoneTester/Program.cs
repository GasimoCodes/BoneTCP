namespace BoneTester
{

    using BoneTCP;
    using BoneTCP.Data;
    using System.Net;
    using System.Text;

    internal class Program
    {

        static void Main(string[] args)
        {

            Server s = new Server(6900, false);
            s.Start();

            s.onMessageReceived += (Message m, IPEndPoint p) =>
            {
                Console.WriteLine("--- Server received: " + m.Data.Substring(0, Math.Clamp(m.Data.Length, 0, 20)));
            };


            Client c = new Client("127.0.0.1", 6900, false, 2048);
            Client e = new Client("127.0.0.1", 6900, false, 1024);

            e.SendMessage("Hello world!!");

            new Thread(() =>
            {
                int i = 0;
                while (i < 20)
                {
                    i++;
                    c.SendMessage("Client A: " + i + "\n " + GetRandomString(2048 * 8));
                }
            }).Start();

            new Thread(() =>
            {
                int i = 0;
                while (i < 5)
                {
                    i++;
                    e.SendMessage("Client B: " + i);
                }
            }).Start();


        }

        internal static string GetRandomString(int stringLength)
        {
            StringBuilder sb = new StringBuilder();
            int numGuidsToConcat = (((stringLength - 1) / 32) + 1);
            for (int i = 1; i <= numGuidsToConcat; i++)
            {
                sb.Append(Guid.NewGuid().ToString("N"));
            }

            return sb.ToString(0, stringLength);
        }

    }
}