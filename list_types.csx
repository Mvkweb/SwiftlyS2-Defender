using System;
using System.Reflection;
using System.Linq;

var assembly = Assembly.LoadFrom("build/net10.0/SwiftlyS2.Shared.dll");
var types = assembly.GetTypes().Where(t => t.Name.Contains("Decal") || t.Name.Contains("Blood")).Select(t => t.Name);
foreach(var t in types) {
    Console.WriteLine(t);
}
