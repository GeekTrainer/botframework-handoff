using System;
using System.Collections.Generic;
using Microsoft.Bot.Schema;

namespace botframework_routing_cs
{
    struct Connection
    {
        public (ConversationReference Ref0, ConversationReference Ref1) References;
    }

    abstract class ConnectionManager
    {
        // Returns the connections that are incomplete (i.e. only have one user, no user on the other end)
        public abstract IList<Connection> GetWaitingConnections();

        // Start a new (waiting) connection for the given reference
        public abstract void StartConnection(ConversationReference reference);

        // Complete a connection that is already waiting
        public abstract void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference);

        // Removes the connection that the given reference is part of
        public abstract void RemoveConnection(ConversationReference reference);

        // Returns whether the given reference is part of a connected connection
        public bool IsConnected(ConversationReference reference)
        {
            Connection? conn = GetConnection(reference);
            return conn != null && conn.Value.References.Ref1 != null;
        }

        // Returns whether the given reference is part of a waiting connection
        public bool IsWaiting(ConversationReference reference)
        {
            Connection? conn = GetConnection(reference);
            return conn != null && conn.Value.References.Ref1 == null;
        }

        // Returns the reference to which the given reference is connected to
        public ConversationReference ConnectedTo(ConversationReference reference)
        {
            Connection? conn = GetConnection(reference);
            if (conn == null)
            {
                return null;
            }

            return AreConversationReferencesEqual(reference, conn.Value.References.Ref0) ? conn.Value.References.Ref1 : conn.Value.References.Ref0;
        }

        // Returns the connection associated with the given reference
        protected abstract Connection? GetConnection(ConversationReference reference);

        // Used internally to determine whether two ConversationReferences refer to the same conversation
        protected bool AreConversationReferencesEqual(ConversationReference ref1, ConversationReference ref2)
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

    class PoolConnectionManager : ConnectionManager
    {
        // Keep track of many connections
        private List<Connection> connections = new List<Connection>();

        public override IList<Connection> GetWaitingConnections()
        {
            // Filter down to connections that have an empty end
            return connections.FindAll(c => c.References.Ref1 == null);
        }

        public override void StartConnection(ConversationReference reference)
        {
            // Ensure reference isn't already part of a connection
            if (GetConnection(reference) != null)
            {
                throw new Exception("Connection already exists");
            }

            // Add a new connection
            connections.Add(new Connection() { References = (reference, null) });
        }

        public override void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference)
        {
            // Find the corresponding waiting connection
            int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(waitingReference, c.References.Ref0) && c.References.Ref1 == null);
            if (connIndex < 0)
            {
                throw new Exception("Connection does not exist");
            }

            // Add the newReference to the other end
            connections[connIndex] = new Connection() { References = (connections[connIndex].References.Ref0, newReference) };
        }

        public override void RemoveConnection(ConversationReference reference)
        {
            int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(reference, c.References.Ref0) || AreConversationReferencesEqual(reference, c.References.Ref1));
            if (connIndex < 0)
            {
                throw new Exception("Connection does not exist");
            }

            connections.RemoveAt(connIndex);
        }

        protected override Connection? GetConnection(ConversationReference reference)
        {
            int connIndex = connections.FindIndex(c => AreConversationReferencesEqual(reference, c.References.Ref0) || AreConversationReferencesEqual(reference, c.References.Ref1));
            if (connIndex < 0)
            {
                return null;
            }

            return connections[connIndex];
        }
    }
}