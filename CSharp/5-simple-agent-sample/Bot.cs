using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
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
            // Welcome message when user or agent joins conversation
            if (context.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (context.Activity.MembersAdded.Any(m => m.Id != context.Activity.Recipient.Id))
                {
                    string msg = IsAgent(context)
                        ? "Say 'list' to list pending users, or say a user ID to connect to"
                        : "Say 'agent' to connect to an agent";
                    await context.SendActivity(msg);
                    return;
                }
            }

            // Ignore non-message activities
            if (context.Activity.Type != ActivityTypes.Message)
            {
                return;
            }

            // If connected, forward activity
            ConversationReference self = TurnContext.GetConversationReference(context.Activity);
            ConversationReference connectedTo = connectionManager.ConnectedTo(self);
            if (connectedTo != null)
            {
                await ForwardTo(context, connectedTo);
                return;
            }

            // Agent code
            if (IsAgent(context))
            {
                IList<Connection> pending = connectionManager.GetWaitingConnections();

                if (context.Activity.Text == "list")
                {
                    // Send agent a list of pending users
                    var pendingStrs = pending.Select(c => $"{c.References.Ref0.User.Name} ({c.References.Ref0.User.Id})");
                    await context.SendActivity(pendingStrs.Count() > 0 ? string.Join("\n\n", pendingStrs) : "No users waiting");
                    return;
                }
                else
                {
                    // Assume the agent said a pending user's id. Find that user
                    // TODO: this is kind of messy b/c Connection is a value type
                    Connection conn = pending.FirstOrDefault(p => p.References.Ref0.User.Id == context.Activity.Text);
                    if (conn.References.Ref0 == null)
                    {
                        await context.SendActivity($"No pending user with id {context.Activity.Text}");
                        return;
                    }

                    // Connect to the pending user
                    connectionManager.CompleteConnection(conn.References.Ref0, self);

                    // Send message to both user and agent
                    await SendTo(context, $"You are connected to {context.Activity.From.Name}", conn.References.Ref0);
                    await context.SendActivity($"You are connected to {conn.References.Ref0.User.Name}");
                    return;
                }
            }

            // User code
            else
            {
                if (context.Activity.Text == "agent")
                {
                    // Start waiting for an agent
                    connectionManager.StartConnection(self);
                    await context.SendActivity("Waiting for an agent... say 'stop' to stop waiting");
                    return;
                }
                else if (context.Activity.Text == "stop")
                {
                    // Stop waiting for an agent
                    connectionManager.RemoveConnection(self);
                    await context.SendActivity("Stopped waiting");
                    return;
                }
                else
                {
                    // Echo bot
                    await context.SendActivity($"You said: {context.Activity.Text}");
                    return;
                }
            }
        }

        private bool IsAgent(ITurnContext context)
        {
            return context.Activity.From.Id == "default-agent";
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
