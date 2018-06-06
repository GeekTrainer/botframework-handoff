# Part 4: Connections for a pool of users

Finally, lets implement a `ConnectionManager` that supports an arbitrary number of users rather than limiting it to two users. The main difference is that we are storing a list of `Connection`s rather than a single `Connection`.

```csharp
class PoolConnectionManager : ConnectionManager
{
    // Keep track of many connections
    private List<Connection> connections = new List<Connection>();

    public override IList<Connection> GetWaitingConnections()
    {
        // Filter down to connections that have an empty end
        return connections.FindAll(c => c.References.Ref1 == null);
    }

    public override void StartConnection(ConversationReference reference)
    {
        // Ensure reference isn't already part of a connection
        if (GetConnection(reference) != null)
        {
            throw new Exception("Connection already exists");
        }

        // Add a new connection
        connections.Add(new Connection() { References = (reference, null) });
    }

    public override void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference)
    {
        // Find the corresponding waiting connection
        int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(waitingReference, c.References.Ref0) && c.References.Ref1 == null);
        if (connIndex < 0)
        {
            throw new Exception("Connection does not exist");
        }

        // Add the newReference to the other end
        connections[connIndex] = new Connection() { References = (connections[connIndex].References.Ref0, newReference) };
    }

    public override void RemoveConnection(ConversationReference reference)
    {
        int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(reference, c.References.Ref0) || AreConversationReferencesEqual(reference, c.References.Ref1));
        if (connIndex < 0)
        {
            throw new Exception("Connection does not exist");
        }

        connections.RemoveAt(connIndex);
    }

    protected override Connection? GetConnection(ConversationReference reference)
    {
        int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(reference, c.References.Ref0) || AreConversationReferencesEqual(reference, c.References.Ref1));
        if (connIndex < 0)
        {
            return null;
        }

        return connections[connIndex];
    }
}
```

In the bot logic, we'll connect users in pairs as they come in. So the 1st user will connect with the 2nd user, the 3rd with the 4th, and so on. The bot logic is nearly identical to before. The only difference is that we no longer have to catch an exception when more than two users try to join.

```csharp
static class Globals
{
    public static ConnectionManager connectionManager = new PoolConnectionManager();
}
```
```csharp
public async Task OnTurn(ITurnContext context)
{
    // Only handle message activities
    if (context.Activity.Type != ActivityTypes.Message) return;

    ConversationReference self = TurnContext.GetConversationReference(context.Activity);

    // If you're connected, forward your message
    ConversationReference otherRef = Globals.connectionManager.ConnectedTo(self);
    if (otherRef != null)
    {
        await ForwardTo(context, otherRef);
        return;
    }

    // If you're waiting, you need to be patient
    if (Globals.connectionManager.IsWaiting(self))
    {
        await context.SendActivity("You are still waiting for someone");
        return;
    }

    // You're new!
    IList<Connection> pending = Globals.connectionManager.GetWaitingConnections();
    if (pending.Count > 0)
    {
        // Found someone to pair you with
        ConversationReference waitingRef = pending[0].References.Ref0;
        Globals.connectionManager.CompleteConnection(waitingRef, self);
        await SendTo(context, "You have been connected to someone who just joined", waitingRef);
        await context.SendActivity("You have been connected to someone who was waiting");
    }
    else
    {
        // No one to pair you with, so you need to wait for someone
        Globals.connectionManager.StartConnection(self);
        await context.SendActivity("You are now waiting for someone");
    }
}
```

Continue to [Part 5: Connect to agent sample](../5-simple-agent-sample/)