using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CouponManagement.Shared.Services
{
    public class ReceiptService
    {
        private readonly CouponContext _context;

        public ReceiptService(CouponContext context)
        {
            _context = context;
        }

        public ReceiptService() : this(new CouponContext())
        {
        }

        // Receipt Methods
        public async Task<List<ReceiptDisplayModel>> GetAllReceiptsAsync()
        {
            var receipts = await _context.Receipts
                .OrderByDescending(r => r.ReceiptDate)
                .ToListAsync();

            return receipts.Select(r => new ReceiptDisplayModel
            {
                ReceiptID = r.ReceiptID,
                ReceiptDate = r.ReceiptDate,
                TotalAmount = r.TotalAmount,
                CustomerName = r.CustomerName,
                CustomerPhoneNumber = r.CustomerPhoneNumber,
                ReceiptCode = r.ReceiptCode,
                SalesPersonId = r.SalesPersonId,
                Status = r.Status,
                PaymentMethodId = r.PaymentMethodId
            }).ToList();
        }

        public async Task<ReceiptModel?> GetReceiptByIdAsync(int id)
        {
            return await _context.Receipts
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.ReceiptID == id);
        }

        public async Task<ReceiptModel> CreateReceiptAsync(ReceiptModel receipt)
        {
            _context.Receipts.Add(receipt);
            await _context.SaveChangesAsync();
            return receipt;
        }

        public async Task<bool> UpdateReceiptStatusAsync(int id, string status)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt == null) return false;

            receipt.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        // Receipt Items Methods
        public async Task<List<DatabaseReceiptItem>> GetReceiptItemsAsync(int receiptId)
        {
            return await _context.ReceiptItems
                .Where(ri => ri.ReceiptId == receiptId)
                .ToListAsync();
        }

        public async Task<DatabaseReceiptItem> AddReceiptItemAsync(DatabaseReceiptItem item)
        {
            _context.ReceiptItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}