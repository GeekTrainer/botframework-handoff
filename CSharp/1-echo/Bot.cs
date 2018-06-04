using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Connector.Authentication;
using System.Security.Claims;

namespace botframework_routing_cs
{
    public class Bot : IBot
    {
        public Task OnTurn(ITurnContext context)
        {
            // Only handle message activities
            if (context.Activity.Type != ActivityTypes.Message) return Task.CompletedTask;
            
            ConversationReference self = TurnContext.GetConversationReference(context.Activity);
            return ForwardTo(context, self);
        }

        private Task SendTo(ITurnContext context, IActivity activity, ConversationReference to)
        {
            string id = context.Services.Get<ClaimsIdentity>("BotIdentity").FindFirst(AuthenticationConstants.AudienceClaim).Value;
            return context.Adapter.ContinueConversation(id, to, async (sendContext) => {
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
    }
}
