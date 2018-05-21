import { Activity, ConversationReference, TurnContext } from 'botbuilder';

export interface Connection {
    refs: [Partial<ConversationReference> | undefined, Partial<ConversationReference> | undefined];
}

export abstract class ConnectionManager {
    // Returns the connections that are incomplete (i.e. only have one user, no user on the other end)
    public abstract getWaitingConnections(): Connection[];
    
    // Start a new (waiting) connection for the given ref
    public abstract startConnection(ref: Partial<ConversationReference>): void;
    
    // Complete a connection that is already waiting
    public abstract completeConnection(waitingRef: Partial<ConversationReference>, newRef: Partial<ConversationReference>): void;

    // Returns whether the given ref is part of a connected connection
    public isConnected(ref: Partial<ConversationReference>): boolean {
        const conn = this.getConnection(ref);
        return conn !== undefined && conn.refs[0] !== undefined && conn.refs[1] !== undefined;
    };
    
    // Returns whether the given ref is part of a waiting connection
    public isWaiting(ref: Partial<ConversationReference>): boolean {
        const conn = this.getConnection(ref);
        return conn !== undefined && (conn.refs[0] === undefined || conn.refs[1] === undefined);
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

export class PoolConnectionManager extends ConnectionManager {
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

export function sendTo(context: TurnContext, activityOrText: Partial<Activity> | string, to: Partial<ConversationReference>) {
    return context.adapter.continueConversation(to, async sendContext => {
        await sendContext.sendActivity(activityOrText);
    });
}

export function forwardTo(context: TurnContext, to: Partial<ConversationReference>) {
    return sendTo(context, context.activity, to);
}