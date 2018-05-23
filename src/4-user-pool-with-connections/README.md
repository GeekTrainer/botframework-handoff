# Part 4: Connections for a pool of users

Finally, lets implement a `ConnectionManager` that supports an arbitrary number of users rather than limiting it to two users. The main difference is that we are storing an array of `Connection`s rather than a single `Connection`.

```ts
class PoolConnectionManager extends ConnectionManager {
    private connections: Connection[] = [];

    // Returns the connections that are incomplete (i.e. only have one user, no user on the other end)
    public getWaitingConnections(): Connection[] {
        return this.connections.filter(c => c.refs[1] === undefined);
    }

    // Start a new (waiting) connection for the given ref
    public startConnection(ref: Partial<ConversationReference>): void {
        // Ensure ref isn't already part of a connection
        if (this.getConnection(ref) !== undefined) {
            throw new Error('Connection already exists');
        }

        // Add a new connection
        this.connections.push({
            refs: [ref, undefined]
        });
    }

    public completeConnection(waitingRef: Partial<ConversationReference>, newRef: Partial<ConversationReference>): void {
        // Find the corresponding waiting connection
        const conn = this.connections.find(c => this.areConversationReferencesEqual(waitingRef, c.refs[0]) && c.refs[1] === undefined);
        if (conn === undefined) {
            throw new Error('Connection does not exist');
        }

        // Add the new ref to the other end
        conn.refs[1] = newRef;
    }

    // // Removes the connection that the given ref is part of
    // // Returns whether it was successful
    // public removeConnection(ref: Partial<ConversationReference>): void {
    //     const i = this.connections.findIndex(c => this.areConversationReferencesEqual(ref, c.refs[0]) || this.areConversationReferencesEqual(ref, c.refs[1]));
    //     if (i < 0) {
    //         throw new Error('Connection does not exist');
    //     }

    //     this.connections.splice(i, 1);
    // }


    // Returns the connection that the given ref is part of, or undefined if it isn't part of any connections
    protected getConnection(ref: Partial<ConversationReference>): Connection | undefined {
        return this.connections.find(c => this.areConversationReferencesEqual(ref, c.refs[0]) || this.areConversationReferencesEqual(ref, c.refs[1]));
    }
}
```

In the bot logic, we'll connect users in pairs as they come in. So the 1st user will connect with the 2nd user, the 3rd with the 4th, and so on. The bot logic is nearly identical to before. The only difference is that we no longer have to catch an exception when more than two users try to join.

```ts
const connectionManager = new PoolConnectionManager();
async function botLogic(context: TurnContext) {
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
        // No one to pair you with, so you need to wait for someone
        connectionManager.startConnection(ref);
        await context.sendActivity(`You are waiting for someone`);
    }
}
```

Continue to [Part 5: Connect to agent sample](../5-simple-agent-sample/)