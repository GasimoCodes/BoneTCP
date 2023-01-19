using System.ComponentModel;
using System.Net;
using System.Text;

namespace BoneTCP
{
    internal class Program
    {
        static void Main(string[] args)
        {

            Message a = new Message("Hello world!!!");
            a.SeqID = 69;

            Fragment[] frags = FragmentWorker.SerializeMessage(a, 8);


            Message m = FragmentWorker.ParseMessage(frags);
            Console.WriteLine(m.Data + " (/ " + m.SeqID);


            /*
            Server s = new Server(6900, true);
            s.Start();


            Client c = new Client("127.0.0.1", 6900, false);
            Client e = new Client("127.0.0.1", 6900, false);
            

            s.onMessageReceived += (Fragment m, IPEndPoint p) => {
                Console.WriteLine("--- Sevr received: " + m.Data);
                // s.SendMessage("HJenlo", p);
            };


            c.onMessageReceived += (Fragment m, IPEndPoint p) => {
                Console.WriteLine("CLNT received: " + m.Data);
            };


            
            new Thread(() =>
            {

                int i = 0;
                while (i < 5)
                {
                    i++;
                    c.SendMessage("Client A: " + i);
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
            
            
            */

            //while (true) ;


        }
    }
}