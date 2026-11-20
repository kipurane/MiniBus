# MiniBus

MiniBus coordinates reliable message processing and outgoing message dispatch.

## Language

**Outgoing operation**
A request to send, publish, or schedule a message as a consequence of message processing.

**Outbox batch**
A group of persisted outgoing operations claimed together for a dispatch attempt. Its outcome accounts for both successfully dispatched operations and failed operations, including those whose message identity cannot be resolved.

**Processing attempt**
One execution of processing for a received message. An immediate retry begins another attempt for the same received message.

**Prepared outgoing message**
An outgoing message whose identity and payload representation have been selected for delivery. Its payload is carried inline or through a claim-check reference.
