# Part 3: Connections

In the previous example, the code for *how* to forward messages (i.e. `ForwardTo()`) didn't change. All of the added code deals with managing the `ConversationReference`s and determining who to forward messages to. With more than two users, this becomes a significant piece of the human-handoff scenario.

It would be helpful to abstract out the logic to answer the question: "Who should I forward this message to?" In the previous example, we tracked a pair of users, and this pair represented a connection between those two users. Tracking many of these pairs (or "connections") is one way to answer the question.

We're making some assumptions with this model:
- A connection is between only two users
- A user can only be in a single connection at a time
- A connection can be in a "waiting" state, where there is a single user in the connection. The connection enters a "connected" state once the second user completes it. This is useful in scenarios where there is a pool of users who are pending connection to others.

A connection is just a pair of `ConversationReference` objects:

```csharp
struct Connection
{
    public (ConversationReference Ref0, ConversationReference Ref1) References;
}
```

And we write an abstract class that manages these connections. This contains the core functionality our model provides around connections.

```csharp
abstract class ConnectionManager
{
    // Returns the connections that are incomplete (i.e. only have one user, no user on the other end)
    public abstract IList<Connection> GetWaitingConnections();

    // Start a new (waiting) connection for the given reference
    public abstract void StartConnection(ConversationReference reference);

    // Complete a connection that is already waiting
    public abstract void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference);

    // Removes the connection that the given reference is part of
    public abstract void RemoveConnection(ConversationReference reference);

    // Returns whether the given reference is part of a connected connection
    public bool IsConnected(ConversationReference reference)
    {
        Connection? conn = GetConnection(reference);
        return conn != null && conn.Value.References.Ref1 != null;
    }

    // Returns whether the given reference is part of a waiting connection
    public bool IsWaiting(ConversationReference reference)
    {
        Connection? conn = GetConnection(reference);
        return conn != null && conn.Value.References.Ref1 == null;
    }

    // Returns the reference to which the given reference is connected to
    public ConversationReference ConnectedTo(ConversationReference reference)
    {
        Connection? conn = GetConnection(reference);
        if (conn == null)
        {
            return null;
        }

        return AreConversationReferencesEqual(reference, conn.Value.References.Ref0) ? conn.Value.References.Ref1 : conn.Value.References.Ref0;
    }

    // Returns the connection associated with the given reference
    protected abstract Connection? GetConnection(ConversationReference reference);

    // Used internally to determine whether two ConversationReferences refer to the same conversation
    protected bool AreConversationReferencesEqual(ConversationReference ref1, ConversationReference ref2)
    {
        return ref1 != null
            && ref2 != null
            && ref1.ChannelId == ref2.ChannelId
            && ref1.User != null && ref2.User != null
            && ref1.User.Id == ref2.User.Id
            && ref1.Conversation != null && ref2.Conversation != null
            && ref1.Conversation.Id == ref2.Conversation.Id;
    }
}
```

Now we can implement a `ConnectionManager` for our previous example, accepting only two users and storing data in memory:

```csharp
class TwoConnectionManager : ConnectionManager
{
    // Keep track of a single connection
    private Connection? connection = null;

    public override IList<Connection> GetWaitingConnections()
    {
        // If only one end of connection is filled, return it
        if (connection != null && connection.Value.References.Ref1 == null)
        {
            return new List<Connection>() { connection.Value };
        }

        return new List<Connection>();
    }

    public override void StartConnection(ConversationReference reference)
    {
        // Ensure there isn't already a started connection
        if (connection != null)
        {
            throw new Exception("Connection already started");
        }

        // Add a new connection
        connection = new Connection() { References = (reference, null) };
    }

    public override void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference)
    {
        // Ensure the waiting connection corresponds to waitingReference
        if (connection == null || connection.Value.References.Ref1 != null || !AreConversationReferencesEqual(waitingReference, connection.Value.References.Ref0))
        {
            throw new Exception("Connection does not exist");
        }

        // Add the new reference to the other end
        connection = new Connection() { References = (connection.Value.References.Ref0, newReference) };
    }

    public override void RemoveConnection(ConversationReference reference)
    {
        if (GetConnection(reference) == null)
        {
            throw new Exception("Connection does not exist");
        }

        connection = null;
    }

    protected override Connection? GetConnection(ConversationReference reference)
    {
        if (connection == null || AreConversationReferencesEqual(reference, connection.Value.References.Ref0) || AreConversationReferencesEqual(reference, connection.Value.References.Ref1))
        {
            return connection;
        }

        return null;
    }
}
```

Then our bot logic becomes:

```csharp
static ConnectionManager connectionManager = new TwoConnectionManager();

public async Task OnTurn(ITurnContext context)
{
    // Only handle message activities
    if (context.Activity.Type != ActivityTypes.Message) return;

    ConversationReference self = TurnContext.GetConversationReference(context.Activity);

    // If you're connected, forward your message
    ConversationReference otherRef = connectionManager.ConnectedTo(self);
    if (otherRef != null)
    {
        await ForwardTo(context, otherRef);
        return;
    }

    // If you're waiting, you need to be patient
    if (connectionManager.IsWaiting(self))
    {
        await context.SendActivity("You are still waiting for someone");
        return;
    }

    // You're new!
    IList<Connection> pending = connectionManager.GetWaitingConnections();
    if (pending.Count > 0)
    {
        // Found someone to pair you with
        ConversationReference waitingRef = pending[0].References.Ref0;
        connectionManager.CompleteConnection(waitingRef, self);
        await SendTo(context, "You have been connected to someone who just joined", waitingRef);
        await context.SendActivity("You have been connected to someone who was waiting");
    }
    else
    {
        // No one to pair you with, so try to wait for someone
        try
        {
            connectionManager.StartConnection(self);
            await context.SendActivity("You are now waiting for someone");
        }
        catch
        {
            // startConnection() threw because there's already a connection
            await context.SendActivity("Sorry, I can't connect you");
        }
    }
}
```

Continue to [Part 4: Connections for a pool of users](../4-user-pool-with-connections/)