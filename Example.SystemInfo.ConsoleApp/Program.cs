#pragma warning disable CA1416
// ReSharper disable UseObjectOrCollectionInitializer
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

using MacDotNet.SystemInfo;

var rootCommand = new RootCommand("Platform info");

//--------------------------------------------------------------------------------
// Uptime
//--------------------------------------------------------------------------------
var uptimeCommand = new Command("uptime", "Get uptime");
uptimeCommand.Handler = CommandHandler.Create(static (IConsole console) =>
{
    var uptime = PlatformProvider.GetUptime();
    console.WriteLine($"Uptime: {uptime.Uptime}");
});
rootCommand.Add(uptimeCommand);

// TODO

//--------------------------------------------------------------------------------
// Run
//--------------------------------------------------------------------------------
rootCommand.Invoke(args);
