using System.Text.Json.Serialization;
using Loco.Core.DurableExecution;
using Loco.Core.Examples;
using Loco.Core.Models;
using Loco.Core.Workflows;

namespace Loco.Core.Serialization;

[JsonSerializable(typeof(WorkflowDefinition))]
[JsonSerializable(typeof(WorkflowStep))]
[JsonSerializable(typeof(Loco.Core.DurableExecution.WorkflowEvent))]
[JsonSerializable(typeof(WorkflowStartedEvent))]
[JsonSerializable(typeof(WorkflowCompletedEvent))]
[JsonSerializable(typeof(WorkflowFailedEvent))]
[JsonSerializable(typeof(ActivityStartedEvent))]
[JsonSerializable(typeof(ActivityCompletedEvent))]
[JsonSerializable(typeof(ActivityFailedEvent))]
[JsonSerializable(typeof(TimerStartedEvent))]
[JsonSerializable(typeof(TimerFiredEvent))]
[JsonSerializable(typeof(ExternalEventReceivedEvent))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(PaymentResult))]
[JsonSerializable(typeof(ShipmentDeliveredEvent))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<Loco.Core.DurableExecution.WorkflowEvent>))]
[JsonSerializable(typeof(List<string>))]
public partial class LocoJsonContext : JsonSerializerContext
{
}
