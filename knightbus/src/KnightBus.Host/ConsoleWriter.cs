using System;

namespace KnightBus.Host
{
    internal class ConsoleWriter
    {
        public static void WriteLine(string s)
        {
            Console.WriteLine($"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}] {s}");
        }

        public static void Write(string s)
        {
            Console.Write($"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}] {s}");
        }
    }
}