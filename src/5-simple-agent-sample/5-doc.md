# Part 5: Connect to agent sample

Let's use the `PoolConnectionManager` implemented previously to write a simple bot to emulate the human-handoff scenario. We have two types of users talking to the bot:
  - **Users** can talk to the bot directly or request to talk to an agent, at which point they are waiting for an agent
  - **Agents** can view this pool of waiting users and choose one with whom to connect

In a real scenario, you could distinguish agents from users in a secure way, such as having agents log in to a web portal and passing their credentials to the bot. For simplicity, we'll identify agents by their ID:

```ts
function isAgent(context: TurnContext) {
    return context.activity.from.id === 'default-agent';
}
```

When someone joins a conversation with the bot, we'll welcome them by telling them what they can do, depending on whether they are a user or an agent:

```ts
if (context.activity.type === ActivityTypes.ConversationUpdate) {
    if (context.activity.membersAdded && context.activity.membersAdded.some(m => m.id !== context.activity.recipient.id)) {
        const msg = isAgent(context)
            ? `Say 'list' to list pending users, or say a user ID to connect to`
            : `Say 'agent' to connect to an agent`;
        return context.sendActivity(msg);
    }
}
```

When we receive a message, we'll immediately check if we're connected to someone, and if so, we'll forward the message:

```ts
const selfRef = TurnContext.getConversationReference(context.activity);
const connectedTo = cm.connectedTo(selfRef);
if (connectedTo) {
    return forwardTo(context, connectedTo);
}
```

Otherwise, we'll act based on the type of person we're talking to. If it's an agent, they can say "list" to see the waiting users:

```ts
const pending = cm.getWaitingConnections();
if (context.activity.text === 'list') {
    const pendingStrs = pending.map(p => `${p.refs[0]!.user!.name} (${p.refs[0]!.user!.id})`);
    return context.sendActivity(pendingStrs.length > 0 ? pendingStrs.join('\n\n') : 'No users waiting');
}
```

Otherwise we assume the agent said the ID of a user they want to connect to:
```ts
else {
    const conn = pending.find(p => p.refs[0]!.user!.id === context.activity.text);
    if (!conn) {
        return context.sendActivity(`No pending user with id ${context.activity.text}`);
    }

    cm.completeConnection(conn.refs[0]!, selfRef);

    // Send message to both user and agent
    await sendTo(context, `You are connected to ${context.activity.from.name}`, conn.refs[0]!);
    return context.sendActivity(`You are connected to ${conn.refs[0]!.user!.name}`);
}
```

On the other hand, if we're talking to a user, we check if they want to talk to an agent:
```ts
if (context.activity.text === 'agent') {
    cm.startConnection(selfRef);
    return context.sendActivity(`Waiting for an agent...`);
}
```

Otherwise we let them talk to the bot normally. This sample is just an echo bot:
```ts
else {
    return context.sendActivity(`You said: ${context.activity.text}`);
}
```