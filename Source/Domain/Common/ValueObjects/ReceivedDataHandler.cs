namespace Watcher.Common.ValueObjects;

public delegate Task ReceivedDataHandler(ArraySegment<byte> data, bool isEndOfData, CancellationToken ct);
