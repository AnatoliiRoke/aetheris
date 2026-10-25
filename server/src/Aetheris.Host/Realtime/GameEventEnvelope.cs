namespace Aetheris.Host.Realtime;

/// <summary>
/// A game event delivered to a client over SignalR.
/// </summary>
/// <param name="Seq">The per-player sequence number, used to recover after a dropped connection.</param>
/// <param name="Type">The event type, for example <c>queue_done</c>.</param>
/// <param name="Payload">The JSON payload of the event.</param>
/// <param name="CreatedAt">The UTC timestamp of the event.</param>
public sealed record GameEventEnvelope(long Seq, string Type, string Payload, DateTimeOffset CreatedAt);
