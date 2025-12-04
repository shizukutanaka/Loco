using System;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.DurableExecution;

namespace Loco.Core.Examples;

/// <summary>
/// 注文処理ワークフローの例
/// Sagaパターンを使用した分散トランザクション
/// </summary>
public class OrderProcessingWorkflow : DurableWorkflow
{
    public override async Task<WorkflowResult> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        var order = Context.GetInput<Order>();

        try
        {
            // 1. 在庫確保 (補償: 在庫解放)
            var inventoryReserved = await Context.ExecuteActivityAsync<bool>(
                "ReserveInventory",
                new { order.ProductId, order.Quantity },
                new ActivityOptions
                {
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 3,
                        BackoffCoefficient = 2.0,
                        InitialInterval = TimeSpan.FromSeconds(1)
                    }
                },
                cancellationToken);

            if (!inventoryReserved)
            {
                return WorkflowResult.Failed("在庫不足");
            }

            // 補償アクション登録: 在庫解放
            Context.RegisterCompensation(async () =>
            {
                await Context.ExecuteActivityAsync<bool>(
                    "ReleaseInventory",
                    new { order.ProductId, order.Quantity },
                    cancellationToken: cancellationToken);
            });

            // 2. 決済処理 (補償: 返金)
            var paymentResult = await Context.ExecuteActivityAsync<PaymentResult>(
                "ProcessPayment",
                new { order.CustomerId, order.Amount },
                new ActivityOptions
                {
                    Timeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(2)
                    }
                },
                cancellationToken);

            if (!paymentResult.Success)
            {
                return WorkflowResult.Failed("決済失敗");
            }

            // 補償アクション登録: 返金
            Context.RegisterCompensation(async () =>
            {
                await Context.ExecuteActivityAsync<bool>(
                    "RefundPayment",
                    new { paymentResult.TransactionId },
                    cancellationToken: cancellationToken);
            });

            // 3. 配送手配
            var shipmentId = await Context.ExecuteActivityAsync<string>(
                "ArrangeShipment",
                new { order.Address, order.ProductId },
                cancellationToken: cancellationToken);

            // 4. 配送完了待機 (外部イベント)
            // await Context.WaitForEventAsync<ShipmentDeliveredEvent>(
            //     "ShipmentDelivered",
            //     timeout: TimeSpan.FromDays(7),
            //     cancellationToken: cancellationToken);

            // 5. 注文完了
            await Context.ExecuteActivityAsync<bool>(
                "CompleteOrder",
                new { order.OrderId, shipmentId },
                cancellationToken: cancellationToken);

            return WorkflowResult.Successful(new
            {
                OrderId = order.OrderId,
                ShipmentId = shipmentId,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            // エラー発生時、補償アクションが自動的に実行される
            return WorkflowResult.Failed($"注文処理失敗: {ex.Message}");
        }
    }
}

/// <summary>
/// 注文データ
/// </summary>
public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// 決済結果
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 配送完了イベント
/// </summary>
public class ShipmentDeliveredEvent
{
    public string ShipmentId { get; set; } = string.Empty;
    public DateTimeOffset DeliveredAt { get; set; }
}
