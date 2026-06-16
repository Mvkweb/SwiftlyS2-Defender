using System;
using System.Reflection;
using System.Linq;

var assembly = Assembly.LoadFrom("build/net10.0/SwiftlyS2.Shared.dll");
var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "KeyBind");
if (type != null)
{
    foreach(var name in Enum.GetNames(type))
    {
        Console.WriteLine(name);
    }
}
