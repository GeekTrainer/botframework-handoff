# Part 2: Forwarding messages to *other* users

Forwarding messages to yourself isn't very useful. Let's forward messages between two different users. For now, we'll only take the first two users that start conversations with the bot and forward their messages to each other. Any users past that will get rejected.

To achieve this, we'll keep track of the `ConversationReference` associated with the first two users:

```csharp
static (ConversationReference Ref0, ConversationReference Ref1) References = (null, null);
```

Note that we're storing the `ConversationReference`s in memory. This will not persist over a restart of the bot, nor will it scale if you have multiple instances running. For simplicity, we'll continue storing in memory, but in practice you'll likely want to use more persistent and scalable storage.

When we receive a message, we'll check if it matches a saved `ConversationReference` and act accordingly. So we need a way to check if two `ConversationReference`s refer to the same conversation:

```csharp
private bool AreConversationReferencesEqual(ConversationReference ref1, ConversationReference ref2)
{
    return ref1 != null
        && ref2 != null
        && ref1.ChannelId == ref2.ChannelId
        && ref1.User != null && ref2.User != null
        && ref1.User.Id == ref2.User.Id
        && ref1.Conversation != null && ref2.Conversation != null
        && ref1.Conversation.Id == ref2.Conversation.Id;
}
```

In the bot logic, if the user is new, we'll keep track of their `ConversationReference` unless we're already tracking two users. In that case, we'll reject the new user.

```csharp
ConversationReference self = TurnContext.GetConversationReference(context.Activity);
bool isRef0 = AreConversationReferencesEqual(self, References.Ref0);
bool isRef1 = AreConversationReferencesEqual(self, References.Ref1);

// If you're a new user...
if (!isRef0 && !isRef1)
{
    // If there''s room for you, add you
    if (References.Ref0 == null)
    {
        References.Ref0 = self;
        isRef0 = true;
    }
    else if (References.Ref1 == null)
    {
        References.Ref1 = self;
        isRef1 = true;
    }

    // Otherwise, reject you
    else
    {
        return context.SendActivity("Go away, there are already 2 users");
    }
}
```

If an already tracked user sends us a message, we'll try to forward it to the other tracked user unless we don't have another user yet. In that case, we'll let the user know that they're alone.

```csharp
=if (isRef0)
{
    // If there's a ref1, forward the message to ref1
    if (References.Ref1 != null)
    {
        return ForwardTo(context, References.Ref1);
    }

    // Otherwise, you're the only user so far
    return context.SendActivity("You're the only user right now");
}

else if (isRef1)
{
    // There should already be a ref0, so forward the message to ref0
    return ForwardTo(context, References.Ref0);
}
```

Storing `ConversationReference`s and continuing conversations are the building blocks necessary to achieve human-handoff scenarios. The following parts show additional abstractions or solutions to common problems in these scenarios.

Continue to [Part 3: Connections](../3-two-users-with-connections/)