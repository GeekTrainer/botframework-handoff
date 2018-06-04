# Part 3: Connections

In the previous example, the code for *how* to forward messages (i.e. `forwardTo()`) didn't change. All of the added code deals with managing the `ConversationReference`s and determining who to forward messages to. With more than two users, this becomes a significant piece of the human-handoff scenario.

It would be helpful to abstract out the logic to answer the question: "Who should I forward this message to?" In the previous example, we tracked a pair of users, and this pair represented a connection between those two users. Tracking many of these pairs (or "connections") is one way to answer the question.

We're making some assumptions with this model:
- A connection is between only two users
- A user can only be in a single connection at a time
- A connection can be in a "waiting" state, where there is a single user in the connection. The connection enters a "connected" state once the second user completes it. This is useful in scenarios where there is a pool of users who are pending connection to others.

A connection is just a pair of `ConversationReference` objects:

```ts
interface Connection {
    refs: [Partial<ConversationReference>, Partial<ConversationReference> | undefined];
}
```

And we write an abstract class that manages these connections. This contains the core functionality our model provides around connections.

```ts
abstract class ConnectionManager {
    // Returns the connections that are incomplete (i.e. only have one user, no user on the other end)
    public abstract getWaitingConnections(): Connection[];

    // Start a new (waiting) connection for the given ref
    public abstract startConnection(ref: Partial<ConversationReference>): void;

    // Complete a connection that is already waiting
    public abstract completeConnection(waitingRef: Partial<ConversationReference>, newRef: Partial<ConversationReference>): void;

    // Removes the connection that the given ref is part of
    public abstract removeConnection(ref: Partial<ConversationReference>): void;

    // Returns whether the given ref is part of a connected connection
    public isConnected(ref: Partial<ConversationReference>): boolean {
        const conn = this.getConnection(ref);
        return conn !== undefined && conn.refs[1] !== undefined;
    };
    
    // Returns whether the given ref is part of a waiting connection
    public isWaiting(ref: Partial<ConversationReference>): boolean {
        const conn = this.getConnection(ref);
        return conn !== undefined && conn.refs[1] === undefined;
    }
    
    // Returns the ref to which the given ref is connected
    public connectedTo(ref: Partial<ConversationReference>): Partial<ConversationReference> | undefined {
        const conn = this.getConnection(ref);
        if (conn === undefined) {
            return undefined;
        }

        return this.areConversationReferencesEqual(ref, conn.refs[0]) ? conn.refs[1] : conn.refs[0];
    }

    // Returns the connection associated with the given ref
    protected abstract getConnection(ref: Partial<ConversationReference>): Connection | undefined;

    // Used internally to determine whether two ConversationReferences refer to the same conversation
    protected areConversationReferencesEqual(ref1: Partial<ConversationReference> | undefined, ref2: Partial<ConversationReference> | undefined) {
        return ref1 !== undefined
            && ref2 !== undefined
            && ref1.channelId === ref2.channelId
            && ref1.user !== undefined && ref2.user !== undefined
            && ref1.user.id === ref2.user.id
            && ref1.conversation !== undefined && ref2.conversation !== undefined
            && ref1.conversation.id === ref2.conversation.id;
    }
}
```

Now we can implement a `ConnectionManager` for our previous example, accepting only two users and storing data in memory:

```ts
class TwoConnectionManager extends ConnectionManager {
    // Keep track of a single connection
    private connection: Connection | undefined = undefined;

    public getWaitingConnections(): Connection[] {
        // If only one end of connection is filled, return it
        if (this.connection && this.connection.refs[1] === undefined) {
            return [this.connection];
        }

        return [];
    }

    public startConnection(ref: Partial<ConversationReference>): void {
        // Ensure there isn't already a started connection
        if (this.connection) {
            throw new Error('Connection already started');
        }

        // Ensure ref isn't already part of a connection
        if (this.getConnection(ref) !== undefined) {
            throw new Error('User already connected already exists');
        }

        // Add a new connection
        this.connection = { refs: [ref, undefined] };
    }

    public completeConnection(waitingRef: Partial<ConversationReference>, newRef: Partial<ConversationReference>): void {
        // Ensure the waiting connection corresponds to waitingRef
        if (!this.connection || this.connection.refs[1] !== undefined || !this.areConversationReferencesEqual(waitingRef, this.connection.refs[0])) {
            throw new Error(`Connection does not exist`);
        }

        // Add the new ref to the other end
        this.connection.refs[1] = newRef;
    }

    public removeConnection(ref: Partial<ConversationReference>) {
        if (this.getConnection(ref) === undefined) {
            throw new Error(`Connection does not exist`);
        }

        this.connection = undefined;
    }

    // Returns the connection that the given ref is part of, or undefined if it isn't part of any connections
    protected getConnection(ref: Partial<ConversationReference>): Connection | undefined {
        if (!this.connection || this.areConversationReferencesEqual(ref, this.connection.refs[0]) || this.areConversationReferencesEqual(ref, this.connection.refs[1])) {
            return this.connection;
        }

        return undefined;
    }
}
```

Then our bot logic becomes:

```ts
const connectionManager = new TwoConnectionManager();
export async function botLogic(context: TurnContext) {
    // Only handle message activities
    if (context.activity.type !== ActivityTypes.Message) return;

    const ref = TurnContext.getConversationReference(context.activity);

    // If you're connected, forward your message
    const otherRef = connectionManager.connectedTo(ref);
    if (otherRef) {
        return forwardTo(context, otherRef);
    }

    // If you're waiting, you need to be patient
    if (connectionManager.isWaiting(ref)) {
        await context.sendActivity(`You are still waiting for someone`);
        return;
    }

    // You're new!
    const pending = connectionManager.getWaitingConnections();
    if (pending.length > 0) {
        // Found someone to pair you with
        const otherRef = pending[0].refs[0];
        connectionManager.completeConnection(otherRef, ref);
        await sendTo(context, `You have been connected to someone who just joined`, otherRef);
        await context.sendActivity(`You have been connected to someone who was waiting`);
    } else {
        // No one to pair you with, so try to wait for someone
        try {
            connectionManager.startConnection(ref);
            await context.sendActivity(`You are now waiting for someone`);
        } catch (e) {
            // startConnection() threw because there's already a connection
            await context.sendActivity(`Sorry, I can't connect you`);
        }
    }
}
```

Continue to [Part 4: Connections for a pool of users](../4-user-pool-with-connections/)