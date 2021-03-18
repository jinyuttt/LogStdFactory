using LogStdFactory;
using System;

namespace logapp
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < 1000; i++)
            {
                //LogProvider.Instance.Info("sss0");
                //var cur = DateTime.Now;
                //LogProvider.Instance.Info<DateTime>("{@DateTime}", cur);

                var p = new Pserson() { Name = "jin" + i, Age = i };
                LogProvider.Instance.Info("{Name}", p.Name);
            }
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
