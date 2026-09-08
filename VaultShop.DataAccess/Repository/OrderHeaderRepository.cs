using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class OrderHeaderRepository : Repository<OrderHeader>, IOrderHeaderRepository
	{
		private ApplicationDbContext _db;
	
		public OrderHeaderRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(OrderHeader obj)
		{
			_db.OrderHeaders.Update(obj);
		}

        public void UpdateStatus(int id, string orderStatus, string? paymentStatus = null)
        {
			var orderFromDb = _db.OrderHeaders.FirstOrDefault(u => u.Id == id);
			if(orderFromDb != null)
			{
				orderFromDb.OrderStatus= orderStatus;
				if (!string.IsNullOrEmpty(paymentStatus))
				{
					orderFromDb.PaymentStatus = paymentStatus;
				}
			}
        }

        public void UpdateStripePaymentId(int id, string sessionId, string paymentIntentId)
        {
            var orderFromDb = _db.OrderHeaders.FirstOrDefault(u => u.Id == id);
			if (orderFromDb == null)
			{
				return;
			}

			if (!string.IsNullOrEmpty(sessionId))
			{
				orderFromDb.SessionId = sessionId;
			}
            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                orderFromDb.PaymentIntentId = paymentIntentId;
				orderFromDb.PaymentDate = DateTime.UtcNow;
            }
        }

		public bool TryClaimOrderConfirmationEmail(int orderId)
		{
			// ponytail: atomic conditional UPDATE — only one concurrent caller wins; provider-agnostic via ExecuteUpdate
			try
			{
				var rows = _db.OrderHeaders
					.Where(o => o.Id == orderId && o.OrderConfirmationEmailSentUtc == null)
					.ExecuteUpdate(s => s.SetProperty(o => o.OrderConfirmationEmailSentUtc, _ => DateTime.UtcNow));
				if (rows == 1)
				{
					var tracked = _db.ChangeTracker.Entries<OrderHeader>().FirstOrDefault(e => e.Entity.Id == orderId);
					if (tracked != null && tracked.Entity.OrderConfirmationEmailSentUtc == null)
					{
						tracked.Entity.OrderConfirmationEmailSentUtc = DateTime.UtcNow;
					}
				}
				return rows == 1;
			}
			catch
			{
				// fallback for providers without ExecuteUpdate (e.g., InMemory in some tests)
				var order = _db.OrderHeaders.FirstOrDefault(o => o.Id == orderId);
				if (order == null || order.OrderConfirmationEmailSentUtc != null) return false;
				order.OrderConfirmationEmailSentUtc = DateTime.UtcNow;
				_db.SaveChanges();
				var tracked = _db.ChangeTracker.Entries<OrderHeader>().FirstOrDefault(e => e.Entity.Id == orderId);
				if (tracked != null) tracked.Entity.OrderConfirmationEmailSentUtc = order.OrderConfirmationEmailSentUtc;
				return true;
			}
		}

		public void ResetOrderConfirmationEmailClaim(int orderId)
		{
			// ponytail: compensating reset — transient SMTP failure must not leave claim stuck forever
			try
			{
				_db.OrderHeaders.Where(o => o.Id == orderId).ExecuteUpdate(s => s.SetProperty(o => o.OrderConfirmationEmailSentUtc, _ => (DateTime?)null));
				var tracked = _db.ChangeTracker.Entries<OrderHeader>().FirstOrDefault(e => e.Entity.Id == orderId);
				if (tracked != null) tracked.Entity.OrderConfirmationEmailSentUtc = null;
			}
			catch
			{
				var order = _db.OrderHeaders.FirstOrDefault(o => o.Id == orderId);
				if (order == null) return;
				order.OrderConfirmationEmailSentUtc = null;
				_db.SaveChanges();
				var tracked = _db.ChangeTracker.Entries<OrderHeader>().FirstOrDefault(e => e.Entity.Id == orderId);
				if (tracked != null) tracked.Entity.OrderConfirmationEmailSentUtc = null;
			}
		}
    }
}
