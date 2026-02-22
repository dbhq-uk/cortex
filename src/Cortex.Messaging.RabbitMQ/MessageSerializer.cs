using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Serialises and deserialises <see cref="MessageEnvelope"/> to/from JSON bytes.
/// The concrete IMessage type name is returned separately for placement in a transport header.
/// </summary>
public static class MessageSerializer
{
    private static readonly JsonSerializerOptions BaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serialises an envelope to a JSON byte array and extracts the message type name.
    /// </summary>
    /// <returns>Tuple of (json bytes, assembly-qualified type name of the IMessage).</returns>
    public static (byte[] Body, string MessageType) Serialize(MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var messageType = envelope.Message.GetType().AssemblyQualifiedName
            ?? throw new InvalidOperationException(
                "Could not resolve assembly-qualified name for message type.");

        var wire = new WireEnvelope
        {
            Message = JsonSerializer.SerializeToElement(envelope.Message, envelope.Message.GetType(), BaseOptions),
            ReferenceCode = envelope.ReferenceCode.Value,
            Context = envelope.Context,
            Priority = envelope.Priority,
            Sla = envelope.Sla
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(wire, BaseOptions);
        return (json, messageType);
    }

    /// <summary>
    /// Deserialises a JSON byte array back into a MessageEnvelope, using the type header
    /// to resolve the concrete IMessage type.
    /// </summary>
    /// <returns>The deserialised envelope, or null if deserialisation fails.</returns>
    public static MessageEnvelope? Deserialize(ReadOnlySpan<byte> body, string messageTypeName)
    {
        var messageType = Type.GetType(messageTypeName);
        if (messageType is null)
        {
            return null;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<WireEnvelope>(body, BaseOptions);
            if (wire is null)
            {
                return null;
            }

            var message = (IMessage?)wire.Message.Deserialize(messageType, BaseOptions);
            if (message is null)
            {
                return null;
            }

            return new MessageEnvelope
            {
                Message = message,
                ReferenceCode = new ReferenceCode(wire.ReferenceCode),
                Context = wire.Context ?? new MessageContext(),
                Priority = wire.Priority,
                Sla = wire.Sla
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Wire format DTO that avoids polymorphic serialisation issues.
    /// The Message property is serialised as a raw JsonElement so it can be
    /// deserialised with the correct concrete type later.
    /// </summary>
    private sealed class WireEnvelope
    {
        public JsonElement Message { get; init; }
        public string ReferenceCode { get; init; } = "";
        public MessageContext? Context { get; init; }
        public MessagePriority Priority { get; init; } = MessagePriority.Normal;
        public TimeSpan? Sla { get; init; }
    }
}
