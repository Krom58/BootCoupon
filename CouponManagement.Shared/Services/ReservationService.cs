using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CouponManagement.Shared.Services
{
    public class ReservationService
    {
        private readonly CouponContext _context;
        public ReservationService(CouponContext context) => _context = context;

        // Try to reserve quantity for a session with TTL. Uses serializable transaction to reduce race conditions.
        public async Task<bool> TryReserveAsync(int couponDefinitionId, string sessionId, int quantity, TimeSpan ttl)
        {
            if (quantity <= 0) return false;

            await _context.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;
            var expiresAt = now.Add(ttl);

            // Use serializable isolation to avoid two transactions both seeing same available amount
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var totalGenerated = await _context.GeneratedCoupons.CountAsync(g => g.CouponDefinitionId == couponDefinitionId);
                var totalUsed = await _context.GeneratedCoupons.CountAsync(g => g.CouponDefinitionId == couponDefinitionId && g.IsUsed);

                var reservedByOthers = await _context.ReservedCoupons
                    .Where(r => r.CouponDefinitionId == couponDefinitionId && r.SessionId != sessionId && (r.ExpiresAt == null || r.ExpiresAt > now))
                    .SumAsync(r => (int?)r.Quantity) ?? 0;

                var available = totalGenerated - totalUsed - reservedByOthers;
                if (available < quantity)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                var existing = await _context.ReservedCoupons.FirstOrDefaultAsync(r => r.CouponDefinitionId == couponDefinitionId && r.SessionId == sessionId);
                if (existing != null)
                {
                    existing.Quantity += quantity;
                    existing.ExpiresAt = expiresAt;
                    existing.CreatedAt = now;
                    _context.ReservedCoupons.Update(existing);
                }
                else
                {
                    _context.ReservedCoupons.Add(new BootCoupon.Models.ReservedCoupon
                    {
                        CouponDefinitionId = couponDefinitionId,
                        SessionId = sessionId,
                        Quantity = quantity,
                        CreatedAt = now,
                        ExpiresAt = expiresAt
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                try { await tx.RollbackAsync(); } catch { }
                throw;
            }
        }

        // Release a specific quantity for a session (reduce or delete)
        public async Task ReleaseReservationAsync(int couponDefinitionId, string sessionId, int quantity)
        {
            if (quantity <= 0) return;

            var existing = await _context.ReservedCoupons.FirstOrDefaultAsync(r => r.CouponDefinitionId == couponDefinitionId && r.SessionId == sessionId);
            if (existing == null) return;
            existing.Quantity -= quantity;
            if (existing.Quantity <= 0) _context.ReservedCoupons.Remove(existing);
            else _context.ReservedCoupons.Update(existing);
            await _context.SaveChangesAsync();
        }

        // Release all reservations for a session (used on exit or after finalize)
        public async Task ReleaseAllReservationsAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            var reservations = _context.ReservedCoupons.Where(r => r.SessionId == sessionId);
            _context.ReservedCoupons.RemoveRange(reservations);
            await _context.SaveChangesAsync();
        }

        // Get currently available count for a coupon definition for this session.
        // This is totalGenerated - totalUsed - reservedByOthers (excluding this session)
        public async Task<int> GetAvailableForSessionAsync(int couponDefinitionId, string sessionId)
        {
            var now = DateTime.UtcNow;
            var totalGenerated = await _context.GeneratedCoupons.CountAsync(g => g.CouponDefinitionId == couponDefinitionId);
            var totalUsed = await _context.GeneratedCoupons.CountAsync(g => g.CouponDefinitionId == couponDefinitionId && g.IsUsed);
            var reservedByOthers = await _context.ReservedCoupons
                .Where(r => r.CouponDefinitionId == couponDefinitionId && r.SessionId != sessionId && (r.ExpiresAt == null || r.ExpiresAt > now))
                .SumAsync(r => (int?)r.Quantity) ?? 0;

            var available = totalGenerated - totalUsed - reservedByOthers;
            return Math.Max(0, available);
        }

        // Get how many items this session has reserved for the given coupon definition
        public async Task<int> GetReservedQuantityForSessionAsync(int couponDefinitionId, string sessionId)
        {
            var now = DateTime.UtcNow;
            var reserved = await _context.ReservedCoupons
                .Where(r => r.CouponDefinitionId == couponDefinitionId && r.SessionId == sessionId && (r.ExpiresAt == null || r.ExpiresAt > now))
                .SumAsync(r => (int?)r.Quantity) ?? 0;
            return Math.Max(0, reserved);
        }
    }
}
