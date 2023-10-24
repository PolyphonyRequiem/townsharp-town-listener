using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.WebApi;

using TownListener;

UserCredential userCredential = new UserCredential(Config.Current.Username, Config.Current.Password);

WebApiUserClient webApiClient = new WebApiUserClient(userCredential);

while (true)
{
    var joinedServers = (await webApiClient.GetJoinedServersAsync()).ToArray();
    for (int i = 0; i < joinedServers.Length; i++)
    {
        var server = joinedServers[i];
        Console.WriteLine($"{server.id}: {server.id} - {server.name}", i, server.id, server.name);
    }

    Console.WriteLine("Which server do you want to connect to?");

    string? serverInput = Console.ReadLine();
    int serverNumber = serverInput is not null ?
        joinedServers[int.Parse(serverInput!)].id :
        -1;

    TownListenerSession listener = new TownListenerSession(userCredential);

    try
    {
        await listener.ConnectAndListen(serverNumber);
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}