# Part 2: Forwarding messages to *other* users

Forwarding messages to yourself isn't very useful. Let's forward messages between two different users. For now, we'll only take the first two users that start conversations with the bot and forward their messages to each other. Any users past that will get rejected.

To achieve this, we'll keep track of the `ConversationReference` associated with the first two users:

```ts
let refs: [Partial<ConversationReference> | null, Partial<ConversationReference> | null] = [null, null];
```

Note that we're storing the `ConversationReference`s in memory. This will not persist over a restart of the bot, nor will it scale if you have multiple instances running. For simplicity, we'll continue storing in memory, but in practice you'll likely want to use more persistent and scalable storage.

When we receive a message, we'll check if it matches a saved `ConversationReference` and act accordingly. So we need a way to check if two `ConversationReference`s refer to the same conversation:

```ts
function areConversationReferencesEqual(ref1: Partial<ConversationReference> | null, ref2: Partial<ConversationReference> | null) {
    return ref1 !== null
        && ref2 !== null
        && ref1.channelId === ref2.channelId
        && ref1.user !== undefined && ref2.user !== undefined
        && ref1.user.id === ref2.user.id
        && ref1.conversation !== undefined && ref2.conversation !== undefined
        && ref1.conversation.id === ref2.conversation.id;
}
```

If the user is new, we'll keep track of their `ConversationReference` unless we're already tracking two users. In that case, we'll reject the new user.

If an already tracked user sends us a message, we'll try to forward it to the other tracked user unless we don't have another user yet. In that case, we'll let the user know that they're alone.

```ts
function botLogic(context: TurnContext) {
    // Only handle message activities
    if (context.activity.type !== ActivityTypes.Message) return;

    const self = TurnContext.getConversationReference(context.activity);
    let isRef0 = areConversationReferencesEqual(self, refs[0]!);
    let isRef1 = areConversationReferencesEqual(self, refs[1]!);

    // If you're a new user...
    if (!isRef0 && !isRef1) {
        // If there's room for you, add you
        if (refs[0] === null) {
            refs[0] = self;
            isRef0 = true;
        } else if (refs[1] === null) {
            refs[1] = self;
            isRef1 = true;
        }

        // Otherwise, reject you
        else {
            return context.sendActivity(`Go away, there are already 2 users`);
        }
    }

    // If you are ref0...
    if (isRef0) {
        // ...and there's a ref1, forward the message to ref1
        if (refs[1]) {
            return forwardTo(context, refs[1]!);
        }

        // Otherwise, you're the only user so far
        return context.sendActivity(`You're the only user right now`);
    }

    // Or if you are ref1...
    else if (isRef1) {
        // There should already be a ref0, so forward the message to ref0
        return forwardTo(context, refs[0]!);
    }
}
```

Storing `ConversationReference`s and continuing conversations are the building blocks necessary to achieve human-handoff scenarios. The following parts show additional abstractions or solutions to common problems in these scenarios.

Continue to [Part 3: Connections](../3-two-users-with-connections/)