using StardewModdingAPI.Events;

namespace PlayerRomance.Net;

public sealed class MessageRouter
{
    private readonly ModEntry mod;
    private readonly HostHandlers hostHandlers;
    private readonly ClientHandlers clientHandlers;

    public MessageRouter(ModEntry mod, HostHandlers hostHandlers, ClientHandlers clientHandlers)
    {
        this.mod = mod;
        this.hostHandlers = hostHandlers;
        this.clientHandlers = clientHandlers;
    }

    public void Handle(ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != this.mod.ModManifest.UniqueID)
        {
            return;
        }

        if (!Enum.TryParse(e.Type, ignoreCase: true, out MessageType type))
        {
            this.mod.Monitor.Log($"[PR.Net] Ignored unknown message type '{e.Type}'.", StardewModdingAPI.LogLevel.Trace);
            return;
        }

        if (this.mod.IsHostPlayer)
        {
            this.hostHandlers.Handle(type, e);
        }

        this.clientHandlers.Handle(type, e);
    }
}
