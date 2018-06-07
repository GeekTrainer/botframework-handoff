import { Activity, TurnContext, ConversationReference, ActivityTypes } from 'botbuilder';

function sendTo(context: TurnContext, activityOrText: Partial<Activity> | string, to: Partial<ConversationReference>) {
    return context.adapter.continueConversation(to, async sendContext => {
        await sendContext.sendActivity(activityOrText);
    });
}

function forwardTo(context: TurnContext, to: Partial<ConversationReference>) {
    return sendTo(context, context.activity, to);
}

// The two references we're tracking
let refs: [Partial<ConversationReference> | null, Partial<ConversationReference> | null] = [null, null];

export function botLogic(context: TurnContext) {
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

function areConversationReferencesEqual(ref1: Partial<ConversationReference> | null, ref2: Partial<ConversationReference> | null) {
    return ref1 !== null
        && ref2 !== null
        && ref1.channelId === ref2.channelId
        && ref1.user !== undefined && ref2.user !== undefined
        && ref1.user.id === ref2.user.id
        && ref1.conversation !== undefined && ref2.conversation !== undefined
        && ref1.conversation.id === ref2.conversation.id;
}