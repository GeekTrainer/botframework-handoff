import { ActivityTypes, TurnContext } from 'botbuilder';
import { PoolConnectionManager, forwardTo, sendTo } from './5-connectionManager';

const conMan = new PoolConnectionManager();
export async function botLogic(context: TurnContext) {
    // Welcome message when user or agent joins conversation
    if (context.activity.type === ActivityTypes.ConversationUpdate) {
        if (context.activity.membersAdded && context.activity.membersAdded.some(m => m.id !== context.activity.recipient.id)) {
            const msg = isAgent(context)
                ? `Say 'list' to list pending users, or say a user ID to connect to`
                : `Say 'agent' to connect to an agent`;
            return context.sendActivity(msg);
        }
    }

    // Ignore non-message activities
    if (context.activity.type !== ActivityTypes.Message) {
        return;
    }

    // If connected, forward activity
    const selfRef = TurnContext.getConversationReference(context.activity);
    const connectedTo = conMan.connectedTo(selfRef);
    if (connectedTo) {
        return forwardTo(context, connectedTo);
    }

    // Agent code
    if (isAgent(context)) {
        const pending = conMan.getWaitingConnections();

        if (context.activity.text === 'list') {
            // Send agent a list of pending users
            const pendingStrs = pending.map(p => `${p.refs[0]!.user!.name} (${p.refs[0]!.user!.id})`);
            return context.sendActivity(pendingStrs.length > 0 ? pendingStrs.join('\n\n') : 'No users waiting');
        } else {
            // Assume the agent said a pending user's id. Find that user
            const conn = pending.find(p => p.refs[0]!.user!.id === context.activity.text);
            if (!conn) {
                return context.sendActivity(`No pending user with id ${context.activity.text}`);
            }

            // Connect to the pending user
            conMan.completeConnection(conn.refs[0]!, selfRef);

            // Send message to both user and agent
            await sendTo(context, `You are connected to ${context.activity.from.name}`, conn.refs[0]!);
            return context.sendActivity(`You are connected to ${conn.refs[0]!.user!.name}`);
        }
    }

    // User code
    else {
        if (context.activity.text === 'agent') {
            // Start waiting for an agent
            conMan.startConnection(selfRef);
            return context.sendActivity(`Waiting for an agent...`);
        } /*else if (context.activity.text === 'stop') {
            // Stop waiting for an agent
            await connectHelper.endConnection(selfRef);
            return context.sendActivity(`Stopped waiting`);
        }*/ else {
            // Echo bot
            return context.sendActivity(`You said: ${context.activity.text}`);
        }
    }
}

// Helper functions
function isAgent(context: TurnContext) {
    return context.activity.from.id === 'default-agent';
}