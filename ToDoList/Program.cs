using System;
using System.IO;
using System.Text;

namespace ToDoList
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
                string currentDir = Directory.GetCurrentDirectory();
                string site = currentDir + @"\site";
                MyHttpServer server = new MyHttpServer(site, 8888);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}