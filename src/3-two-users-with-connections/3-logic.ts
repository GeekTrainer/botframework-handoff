import { ActivityTypes, TurnContext } from 'botbuilder';
import { TwoConnectionManager, forwardTo, sendTo } from './3-connectionManager';

const conMan = new TwoConnectionManager();
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
        const otherRef = pending[0].refs[0]!;
        conMan.completeConnection(otherRef, ref);
        await sendTo(context, `You have been connected to someone who just joined`, otherRef);
        await context.sendActivity(`You have been connected to someone who was waiting`);
    } else {
        // No one to pair you with, so try to wait for someone
        try {
            conMan.startConnection(ref);
            await context.sendActivity(`You are now waiting for someone`);
        } catch (e) {
            // startConnection() threw because there's already a connection
            await context.sendActivity(`Sorry, I can't connect you`);
        }
    }
}