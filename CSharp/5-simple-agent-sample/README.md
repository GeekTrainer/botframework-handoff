# Part 5: Connect to agent sample

Let's use the `PoolConnectionManager` implemented previously to write a simple bot to emulate the human-handoff scenario. We have two types of users talking to the bot:
  - **Users** can talk to the bot directly or request to talk to an agent, at which point they are waiting for an agent
  - **Agents** can view this pool of waiting users and choose one with whom to connect

In a real scenario, you could distinguish agents from users in a secure way, such as having agents log in to a web portal and passing their credentials to the bot. For simplicity, we'll identify agents by their ID:

```csharp
private bool IsAgent(ITurnContext context)
{
    return context.Activity.From.Id == "default-agent";
}
```

When someone joins a conversation with the bot, we'll welcome them by telling them what they can do, depending on whether they are a user or an agent:

```csharp
if (context.Activity.Type == ActivityTypes.ConversationUpdate)
{
    if (context.Activity.MembersAdded.Any(m => m.Id != context.Activity.Recipient.Id))
    {
        string msg = IsAgent(context)
            ? "Say 'list' to list pending users, or say a user ID to connect to"
            : "Say 'agent' to connect to an agent";
        await context.SendActivity(msg);
        return;
    }
}
```

When we receive a message, we'll immediately check if we're connected to someone, and if so, we'll forward the message:

```csharp
ConversationReference self = TurnContext.GetConversationReference(context.Activity);
ConversationReference connectedTo = connectionManager.ConnectedTo(self);
if (connectedTo != null)
{
    await ForwardTo(context, connectedTo);
    return;
}
```

Otherwise, we'll act based on the type of person we're talking to. If it's an agent, they can say "list" to see the waiting users:

```csharp
IList<Connection> pending = connectionManager.GetWaitingConnections();

if (context.Activity.Text == "list")
{
    var pendingStrs = pending.Select(c => $"{c.References.Ref0.User.Name} ({c.References.Ref0.User.Id})");
    await context.SendActivity(pendingStrs.Count() > 0 ? string.Join("\n\n", pendingStrs) : "No users waiting");
    return;
}
```

Otherwise we assume the agent said the ID of a user they want to connect to:
```csharp
else
{
    // TODO: this is kind of messy b/c Connection is a value type
    Connection conn = pending.FirstOrDefault(p => p.References.Ref0.User.Id == context.Activity.Text);
    if (conn.References.Ref0 == null)
    {
        await context.SendActivity($"No pending user with id {context.Activity.Text}");
        return;
    }

    connectionManager.CompleteConnection(conn.References.Ref0, self);

    // Send message to both user and agent
    await SendTo(context, $"You are connected to {context.Activity.From.Name}", conn.References.Ref0);
    await context.SendActivity($"You are connected to {conn.References.Ref0.User.Name}");
    return;
}
```

On the other hand, if we're talking to a user, we check if they want to talk to an agent:
```csharp
if (context.Activity.Text == "agent")
{
    connectionManager.StartConnection(self);
    await context.SendActivity("Waiting for an agent... say 'stop' to stop waiting");
    return;
}
```

The user may want to stop waiting for an agent:
```csharp
else if (context.Activity.Text == "stop")
{
    connectionManager.RemoveConnection(self);
    await context.SendActivity("Stopped waiting");
    return;
}
```

Otherwise we let them talk to the bot normally. This sample is just an echo bot:
```csharp
else
{
    await context.SendActivity($"You said: {context.Activity.Text}");
    return;
}
```