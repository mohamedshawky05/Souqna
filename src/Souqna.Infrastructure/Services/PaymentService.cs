using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;

namespace Souqna.Infrastructure.Services
{
    public class PaymentService : Core.Interfaces.IPaymentService
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(SouqnaDbContext dbContext, ILogger<PaymentService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task<SubscriptionPaymentResult> ProcessSubscriptionPaymentAsync(
    int subscriptionId,
    decimal amount,
    string currency,
    PaymentMethod paymentMethod)
{
    // تعطيل بوابة الدفع التجريبية.
    // تأكيد الدفع اليدوي يتم من لوحة الإدارة فقط.
    return Task.FromResult(new SubscriptionPaymentResult
    {
        Success = false,
        Message = "الدفع التجريبي معطل. يجب تأكيد الدفع من لوحة الإدارة."
    });
}

public Task<bool> VerifyWebhookSignatureAsync(
    string payload,
    string signature)
{
    // لا نقبل Webhook قبل إعداد تحقق حقيقي من مزود الدفع.
    return Task.FromResult(false);
}
    }
}
