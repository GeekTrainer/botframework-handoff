import { Activity, TurnContext, ConversationReference, ActivityTypes } from 'botbuilder';

function sendTo(context: TurnContext, activityOrText: Partial<Activity> | string, to: Partial<ConversationReference>) {
    return context.adapter.continueConversation(to, async sendContext => {
        await sendContext.sendActivity(activityOrText);
    });
}

function forwardTo(context: TurnContext, to: Partial<ConversationReference>) {
    return sendTo(context, context.activity, to);
}

export function botLogic(context: TurnContext) {
    // Only handle message activities
    if (context.activity.type !== ActivityTypes.Message) return;

    const self = TurnContext.getConversationReference(context.activity);
    return forwardTo(context, self);
}