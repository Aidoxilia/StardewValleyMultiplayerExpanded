using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.UI;

public sealed class RequestPromptService
{
    private readonly ModEntry mod;
    private readonly Queue<PromptEntry> queue = new();
    private readonly HashSet<string> pendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private string? activeKey;

    private sealed class PromptEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Func<(bool success, string message)> OnAccept { get; set; } = null!;
        public Func<(bool success, string message)> OnReject { get; set; } = null!;
        public string Category { get; set; } = "[PR.Core]";
    }

    public RequestPromptService(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Clear()
    {
        this.queue.Clear();
        this.pendingKeys.Clear();
        this.activeKey = null;
    }

    public void Enqueue(
        string key,
        string title,
        string body,
        Func<(bool success, string message)> onAccept,
        Func<(bool success, string message)> onReject,
        string category)
    {
        if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!this.pendingKeys.Add(key))
        {
            return;
        }

        this.queue.Enqueue(new PromptEntry
        {
            Key = key,
            Title = title,
            Body = body,
            OnAccept = onAccept,
            OnReject = onReject,
            Category = category
        });
        this.mod.Monitor.Log($"[PR.UI] Queued consent prompt: {key}", LogLevel.Trace);
    }

    public void TryShowNextPrompt()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (this.activeKey is not null && Game1.activeClickableMenu is null)
        {
            this.pendingKeys.Remove(this.activeKey);
            this.activeKey = null;
        }

        if (this.activeKey is not null || Game1.activeClickableMenu is not null || this.queue.Count == 0)
        {
            return;
        }

        PromptEntry next = this.queue.Dequeue();
        this.activeKey = next.Key;
        int waitingCount = this.queue.Count;

        Game1.activeClickableMenu = new ConsentPromptMenu(
            waitingCount > 0 ? $"{next.Title} ({waitingCount} queued)" : next.Title,
            next.Body,
            () => this.Resolve(next, accept: true),
            () => this.Resolve(next, accept: false),
            () => this.OnPromptClosed(next.Key));
        Game1.playSound("newArtifact");
    }

    private void Resolve(PromptEntry prompt, bool accept)
    {
        (bool success, string message) = accept ? prompt.OnAccept() : prompt.OnReject();
        if (success)
        {
            this.mod.Notifier.NotifyInfo(message, prompt.Category);
        }
        else
        {
            this.mod.Notifier.NotifyWarn(message, prompt.Category);
        }
    }

    private void OnPromptClosed(string key)
    {
        this.pendingKeys.Remove(key);
        if (string.Equals(this.activeKey, key, StringComparison.OrdinalIgnoreCase))
        {
            this.activeKey = null;
        }
    }
}
