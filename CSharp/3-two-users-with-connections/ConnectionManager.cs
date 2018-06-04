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

    // ConnectionManager implemented for the previous case of two users
    class TwoConnectionManager : ConnectionManager
    {
        // Keep track of a single connection
        private Connection? connection = null;

        public override IList<Connection> GetWaitingConnections()
        {
            // If only one end of connection is filled, return it
            if (connection != null && connection.Value.References.Ref1 == null)
            {
                return new List<Connection>() { connection.Value };
            }

            return new List<Connection>();
        }

        public override void StartConnection(ConversationReference reference)
        {
            // Ensure there isn't already a started connection
            if (connection != null)
            {
                throw new Exception("Connection already started");
            }

            // Add a new connection
            connection = new Connection() { References = (reference, null) };
        }

        public override void CompleteConnection(ConversationReference waitingReference, ConversationReference newReference)
        {
            // Ensure the waiting connection corresponds to waitingReference
            if (connection == null || connection.Value.References.Ref1 != null || !AreConversationReferencesEqual(waitingReference, connection.Value.References.Ref0))
            {
                throw new Exception("Connection does not exist");
            }

            // Add the new reference to the other end
            connection = new Connection() { References = (connection.Value.References.Ref0, newReference) };
        }

        public override void RemoveConnection(ConversationReference reference)
        {
            if (GetConnection(reference) == null)
            {
                throw new Exception("Connection does not exist");
            }

            connection = null;
        }

        protected override Connection? GetConnection(ConversationReference reference)
        {
            if (connection == null || AreConversationReferencesEqual(reference, connection.Value.References.Ref0) || AreConversationReferencesEqual(reference, connection.Value.References.Ref1))
            {
                return connection;
            }

            return null;
        }
    }
}