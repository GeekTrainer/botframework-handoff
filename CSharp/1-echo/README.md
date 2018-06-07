# Part 1: Building blocks

The core data in bot-to-human connections is the address that represents a user's conversation with the bot. This is represented by `ConversationReference` in the SDK. During a turn, you can get the `ConversationReference` of the current conversation using `getConversationReference()` on `TurnContext`:

```csharp
TurnContext.GetConversationReference(context.Activity);
```

This contains fields that uniquely identify a conversation, including `User.Id`, `Conversation.Id`, and `ChannelId`.

The core action in bot-to-human connections is the bot forwarding incoming messages to a human. We can continue a conversation with a previously stored `ConversationReference` using `ContinueConversation()` on a `BotAdapter`. For example, say we have two humans talking to the bot, each represented by a `ConversationReference`. On any given turn with one of the humans, we can send an activity to the other human:

```csharp
ConversationReference otherHumanRef = ... // stored previously
Activity activity = ... // any activity
string id = context.Services.Get<ClaimsIdentity>("BotIdentity").FindFirst(AuthenticationConstants.AudienceClaim).Value;
context.Adapter.ContinueConversation(id, otherHumanRef, async (sendContext) => {
    await sendContext.SendActivity(activity);
});
```

We'll use this a lot, so let's wrap it in a function:

```csharp
private Task SendTo(ITurnContext context, IActivity activity, ConversationReference to)
{
    string id = context.Services.Get<ClaimsIdentity>("BotIdentity").FindFirst(AuthenticationConstants.AudienceClaim).Value;
    return context.Adapter.ContinueConversation(id, to, async (sendContext) =>
    {
        await sendContext.SendActivity(activity);
    });
}
```

Sometimes the activity we want to send is exactly the incoming activity, which we can make a bit more convenient:

```csharp
private Task ForwardTo(ITurnContext context, ConversationReference to)
{
    return SendTo(context, context.Activity, to);
}
```

With this, we can write a bot that forwards your messages back to you - a convoluted echo bot!

```csharp
public Task OnTurn(ITurnContext context)
{
    // Only handle message activities
    if (context.Activity.Type != ActivityTypes.Message) return Task.CompletedTask;
    
    ConversationReference self = TurnContext.GetConversationReference(context.Activity);
    return ForwardTo(context, self);
}
```

Continue to [Part 2: Forwarding messages to *other* users](../2-two-users/)