using System.Collections.Generic;
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
        static ConnectionManager connectionManager = new PoolConnectionManager();

        public async Task OnTurn(ITurnContext context)
        {
            // Only handle message activities
            if (context.Activity.Type != ActivityTypes.Message) return;

            ConversationReference self = TurnContext.GetConversationReference(context.Activity);

            // If you're connected, forward your message
            ConversationReference otherRef = connectionManager.ConnectedTo(self);
            if (otherRef != null)
            {
                await ForwardTo(context, otherRef);
                return;
            }

            // If you're waiting, you need to be patient
            if (connectionManager.IsWaiting(self))
            {
                await context.SendActivity("You are still waiting for someone");
                return;
            }

            // You're new!
            IList<Connection> pending = connectionManager.GetWaitingConnections();
            if (pending.Count > 0)
            {
                // Found someone to pair you with
                ConversationReference waitingRef = pending[0].References.Ref0;
                connectionManager.CompleteConnection(waitingRef, self);
                await SendTo(context, "You have been connected to someone who just joined", waitingRef);
                await context.SendActivity("You have been connected to someone who was waiting");
            }
            else
            {
                // No one to pair you with, so you need to wait for someone
                connectionManager.StartConnection(self);
                await context.SendActivity("You are now waiting for someone");
            }
        }

        private Task SendTo(ITurnContext context, IActivity activity, ConversationReference to)
        {
            string id = context.Services.Get<ClaimsIdentity>("BotIdentity").FindFirst(AuthenticationConstants.AudienceClaim).Value;
            return context.Adapter.ContinueConversation(id, to, async (sendContext) =>
            {
                await sendContext.SendActivity(activity);
            });
        }

        private Task SendTo(ITurnContext context, string text, ConversationReference to)
        {
            Activity activity = new Activity(ActivityTypes.Message) { Text = text };
            return SendTo(context, activity, to);
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
