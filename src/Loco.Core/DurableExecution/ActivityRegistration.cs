using Loco.Core.DurableExecution.Activities;

namespace Loco.Core.DurableExecution;

public static class ActivityRegistration
{
    private static bool _isRegistered = false;
    private static readonly object _lock = new();

    public static void RegisterAll()
    {
        if (_isRegistered) return;

        lock (_lock)
        {
            if (_isRegistered) return;

            StaticActivityRegistry.Register(new ReserveInventoryActivity());
            StaticActivityRegistry.Register(new ProcessPaymentActivity());
            StaticActivityRegistry.Register(new ReleaseInventoryActivity());
            StaticActivityRegistry.Register(new RefundPaymentActivity());
            StaticActivityRegistry.Register(new ArrangeShipmentActivity());
            StaticActivityRegistry.Register(new CompleteOrderActivity());

            _isRegistered = true;
        }
    }
}
