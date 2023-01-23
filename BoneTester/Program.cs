namespace BoneTester
{

    using BoneTCP;
    using System.Net;
    using System.Text;

    internal class Program
    {

        static void Main(string[] args)
        {
            /*
            Message a = new Message("");
            a.SeqID = 69;

            Fragment[] frags = FragmentWorker.SerializeMessage(a, 1024);


            Message m = FragmentWorker.ParseMessage(frags);
            Console.WriteLine(m.Data + " / " + m.SeqID);
            */


            Server s = new Server(6900, true);
            s.Start();


            s.onMessageReceived += (Message m, IPEndPoint p) =>
            {
                Console.WriteLine("--- Sevr received: " + m.Data.Substring(0, Math.Clamp(m.Data.Length, 0, 20)));
                //s.SendMessage("HJenlo", p);
            };

            Client c = new Client("127.0.0.1", 6900, true);


            c.onMessageReceived += (Message m, IPEndPoint p) =>
            {
                Console.WriteLine("CLNT received: " + m.Data);
            };


            // Client e = new Client("127.0.0.1", 6900, true);


            new Thread(() =>
            {

                int i = 0;
                while (i < 2)
                {
                    i++;
                    c.SendMessage("Client A: " + i + "\n " + GetRandomString(2048 * 8));
                }

            }).Start();



            /*
            
            new Thread(() =>
            {

                int i = 0;
                while (i < 5)
                {
                    i++;
                    e.SendMessage("Client B: " + i);
                }

            }).Start();
            
            */


            //while (true) ;


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