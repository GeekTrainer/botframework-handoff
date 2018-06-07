using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

namespace botframework_routing_cs
{
    public class Bot : IBot
    {
        static (ConversationReference Ref0, ConversationReference Ref1) References = (null, null);

        public Task OnTurn(ITurnContext context)
        {
            // Only handle message activities
            if (context.Activity.Type != ActivityTypes.Message) return Task.CompletedTask;

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

            // If you are ref0...
            if (isRef0)
            {
                // ...and there's a ref1, forward the message to ref1
                if (References.Ref1 != null)
                {
                    return ForwardTo(context, References.Ref1);
                }

                 // Otherwise, you're the only user so far
                 return context.SendActivity("You're the only user right now");
            }

            // Or if you are ref1...
            else if (isRef1)
            {
                // There should already be a ref0, so forward the message to ref0
                return ForwardTo(context, References.Ref0);
            }

            return Task.CompletedTask;
        }

        private Task SendTo(ITurnContext context, IActivity activity, ConversationReference to)
        {
            string id = context.Services.Get<ClaimsIdentity>("BotIdentity").FindFirst(AuthenticationConstants.AudienceClaim).Value;
            return context.Adapter.ContinueConversation(id, to, async (sendContext) => {
                await sendContext.SendActivity(activity);
            });
        }

        private Task ForwardTo(ITurnContext context, ConversationReference to)
        {
            return SendTo(context, context.Activity, to);
        }

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
    }
}
