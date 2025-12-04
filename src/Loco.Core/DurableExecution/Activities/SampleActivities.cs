using System;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Examples;
using Loco.Core.Serialization;
using System.Text.Json;

namespace Loco.Core.DurableExecution.Activities;

public class ReserveInventoryActivity : IActivity
{
    public string Name => "ReserveInventory";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        // 入力データの型変換 (JSONからの復元を考慮)
        if (input is JsonElement jsonElement)
        {
            // 実際には型を指定してデシリアライズする必要がありますが、
            // ここでは簡略化のため動的にプロパティにアクセスするか、
            // 専用の入力クラスにデシリアライズします。
            // 今回はデモ用なので常にtrueを返します。
        }

        // ロジックシミュレーション
        return Task.FromResult<object?>(true);
    }
}

public class ProcessPaymentActivity : IActivity
{
    public string Name => "ProcessPayment";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>(new PaymentResult 
        { 
            Success = true, 
            TransactionId = Guid.NewGuid().ToString() 
        });
    }
}

public class ReleaseInventoryActivity : IActivity
{
    public string Name => "ReleaseInventory";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>(true);
    }
}

public class RefundPaymentActivity : IActivity
{
    public string Name => "RefundPayment";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>(true);
    }
}

public class ArrangeShipmentActivity : IActivity
{
    public string Name => "ArrangeShipment";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>($"SHIP-{Guid.NewGuid().ToString().Substring(0, 8)}");
    }
}

public class CompleteOrderActivity : IActivity
{
    public string Name => "CompleteOrder";

    public Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>(true);
    }
}
