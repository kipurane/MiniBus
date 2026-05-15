namespace MiniBus.Core.ClaimCheck;

public sealed record MiniBusOutgoingMessage(
    BinaryData Body,
    IReadOnlyDictionary<string, string> Headers,
    bool IsClaimChecked);
