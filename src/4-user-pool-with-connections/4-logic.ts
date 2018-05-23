import { ActivityTypes, TurnContext } from 'botbuilder';
import { PoolConnectionManager, forwardTo, sendTo } from './4-connectionManager';

const conMan = new PoolConnectionManager();
export async function botLogic(context: TurnContext) {
    // Only handle message activities
    if (context.activity.type !== ActivityTypes.Message) return;

    const ref = TurnContext.getConversationReference(context.activity);

    // If you're connected, forward your message
    const otherRef = conMan.connectedTo(ref);
    if (otherRef) {
        return forwardTo(context, otherRef);
    }

    // If you're waiting, you need to be patient
    if (conMan.isWaiting(ref)) {
        await context.sendActivity(`You are still waiting for someone`);
        return;
    }

    // You're new!
    const pending = conMan.getWaitingConnections();
    if (pending.length > 0) {
        // Found someone to pair you with
        const otherRef = pending[0].refs[0];
        conMan.completeConnection(otherRef, ref);
        await sendTo(context, `You have been connected to someone who just joined`, otherRef);
        await context.sendActivity(`You have been connected to someone who was waiting`);
    } else {
        // No one to pair you with, so you need to wait for someone
        conMan.startConnection(ref);
        await context.sendActivity(`You are waiting for someone`);
    }
}