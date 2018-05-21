# Part 1: Building blocks

The core data in bot-to-human connections is the address that represents a user's conversation with the bot. This is represented by `ConversationReference` in the SDK. During a turn, you can get the `ConversationReference` of the current conversation using `getConversationReference()` on `TurnContext`:

```ts
TurnContext.getConversationReference(context.activity);
```

This contains fields that uniquely identify a conversation, including `user.id`, `conversation.id`, and `channelId`.

The core action in bot-to-human connections is the bot forwarding incoming messages to a human. We can continue a conversation with a previously stored `ConversationReference` using `continueConversation()` on a `BotAdapter`. For example, say we have two humans talking to the bot, each represented by a `ConversationReference`. On any given turn with one of the humans, we can send an activity to the other human:

```ts
const otherHumanRef: Partial<ConversationReference> = ... // stored previously
const activity: Partial<Activity> = ... // any activity
context.adapter.continueConversation(otherHumanRef, async sendContext => {
    await sendContext.sendActivity(activity);
});
```

We'll use this a lot, so let's wrap it in a function:

```ts
function sendTo(context: TurnContext, activityOrText: Partial<Activity> | string, to: Partial<ConversationReference>) {
    return context.adapter.continueConversation(to, async sendContext => {
        await sendContext.sendActivity(activityOrText);
    });
}
```

Sometimes the activity we want to send is exactly the incoming activity, which we can make a bit more convenient:

```ts
function forwardTo(context: TurnContext, to: Partial<ConversationReference>) {
    return sendTo(context, context.activity, to);
}
```

With this, we can write a bot that forwards your messages back to you - a convoluted echo bot!

```ts
function botLogic(context: TurnContext) {
    // Only handle message activities
    if (context.activity.type !== ActivityTypes.Message) return;

    const self = TurnContext.getConversationReference(context.activity);
    return forwardTo(context, self);
}
```

Continue to [Part 2: Forwarding messages to *other* users](../2-two-users/)