using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Pastel;

namespace BoneTCP
{
    /// <summary>
    /// Logging tool used internally by SlidingWindow.cs
    /// </summary>
    internal static class SliderLogger
    {
        static Dictionary<string, ConsoleColor> assign = new Dictionary<string, ConsoleColor>();

        public static void Log(string message, string from, string to, int? msgID = null)
        {
            string msgIDCont = "";

            if(msgID != null)
            {
                msgIDCont = "[" + msgID + "]";
            }

            Console.WriteLine($"[{checkRegisterAssoc(from)} -> {checkRegisterAssoc(to)}] {msgIDCont} {message}");
        }



        public static void Log(string message, UdpClient client, string to, int? msgID = null)
        {
            IPEndPoint ipe = ((IPEndPoint)client.Client.LocalEndPoint);
            Log(message, ipe.Port.ToString(), to, msgID);

        }

        public static void LogError(string message, UdpClient client, string to, int? msgID = null)
        {
            IPEndPoint ipe = ((IPEndPoint)client.Client.LocalEndPoint);
            Log(message.Pastel(ConsoleColor.Red), ipe.Port.ToString(), to, msgID);

        }

        static string checkRegisterAssoc(string term)
        {
            if (!assign.ContainsKey(term))
            {
                try { 
                assign.Add(term, (ConsoleColor)(new Random()).Next(0, 15));
                } catch { return term.PastelBg(assign[term]); }
            }

            return term.PastelBg(assign[term]);
        }


    }
}
