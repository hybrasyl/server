using System;
using System.Reflection;
using Hybrasyl.Xml;

namespace XMLTest
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("> ");
                var cmd = Console.ReadLine();
                var cmdarr = cmd.Split(" ");
                Assembly assem = typeof(Hybrasyl.Xml.Access).Assembly;

                switch (cmdarr[0])
                {
                    // load Spawn butt.xml
                    case "load":
                        var type = assem.GetType($"Hybrasyl.Xml.{cmdarr[1]}");
                        if (type is null)
                        {
                            Console.WriteLine("no such type bro");
                            break;
                        }
                        var mi = type.GetMethod("LoadFromFile", new Type[] { typeof(string), 
                            type.MakeByRefType(), 
                            typeof(Exception).MakeByRefType() });
                        if (mi is null)
                        {
                            Console.WriteLine($"Type {cmdarr[1]} is missing a LoadFromFile method");
                            break;
                        }
                        var methodArgs = new object[] { $"C:\\Users\\justin.baugh\\Documents\\Hybrasyl\\world\\xml\\{cmdarr[2]}", 
                            null, null };
                        object result = mi.Invoke(null, methodArgs);
                        bool blResult = (bool)result;
                        if (blResult)
                        {
                            Console.WriteLine("Success");
                            Console.WriteLine($"{methodArgs[1].ToString()}");
                        }
                        else
                        {
                            Console.WriteLine($"yo this is fucked: {methodArgs[2].ToString()}");
                        }

                        break;
                    default:
                        Console.WriteLine("nah");
                        break;
                }
            }
        }
    }
}
