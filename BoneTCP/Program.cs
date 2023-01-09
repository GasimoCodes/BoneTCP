using System.Net;

namespace BoneTCP
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server s = new Server(6900, false);
            s.Start();


            Client c = new Client("127.0.0.1", 6900, true);
            Client e = new Client("127.0.0.1", 6900, false);

            int i = 0;
            while (i < 100)
            {
                i++;
                c.SendMessage("Message" + i);
            }

            //while (true) ;
            

        }
    }
}